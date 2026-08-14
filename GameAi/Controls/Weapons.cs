using CommonUtilities._3DHelpers;
using Domain;
using System;
using System.Collections.Generic;
using System.Net.Security;
using static Domain.WeaponHelpers;
using static Domain._3dSpecificsImplementations;
using CommonUtilities.CommonSetup;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;

namespace GameAiAndControls.Controls
{
    public class Weapons : IWeapon
    {
        private static bool enableLogging = false;
        private static readonly int maxZ = 1200;
        private static readonly int minZ = -2500;
        private static int maxX => (int)(ScreenSetup.screenSizeX * 1.5f);
        private static int minX => (int)(ScreenSetup.screenSizeX * -1.5f);
        private static int maxY => (int)(ScreenSetup.screenSizeY * 4.0f);
        private static int minY => (int)(ScreenSetup.screenSizeY * -4.0f);

        // Audio references are initialized lazily from ConfigureAudio.
        private IAudioPlayer? _audio;
        private SoundDefinition? _thudSound;
        private SoundDefinition? _lazerSound;
        private IAudioInstance? _thudInstance;
        private IAudioInstance? _lazerInstance;

        private readonly List<I3dObject> _weaponObjects;
        private readonly _3dRotationCommon _rotate = new(); // Rotation fra CommonHelpers

        public IObjectMovement ParentShip { get; set; }
        public _3dObject ParentShipObject { get; set; }
        public IVector3 WorldPosition { get; set; } = new Vector3(0, 0, 0);
        public IVector3 ParentVelocityLocal { get; set; } = new Vector3(0, 0, 0);
        public List<IActiveWeapon> ActiveWeapons { get; set; } = new List<IActiveWeapon>();

        private _3dObject? _aimAssistLockedTarget;

        public bool ShowAimAssist { get; set; } = true;
        public bool FireAsEnemyWeapon { get; set; } = false;
        public string EnemyLazerName { get; set; } = "EnemyLazer";
        public float? LazerVelocityOverride { get; set; } = null;
        public float? LazerMaxRangeOverride { get; set; } = null;
        public float? LazerLifetimeSecondsOverride { get; set; } = null;

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            // Already configured? do nothing
            if (_audio != null || _thudSound != null)
                return;

            if (audioPlayer == null || soundRegistry == null)
                return;

            _audio = audioPlayer;
            _thudSound = soundRegistry.Get("lazer_thud");
            _lazerSound = soundRegistry.Get("lazer_main");
        }

        public Weapons(List<I3dObject> weapons, IObjectMovement parent, _3dObject ship)
        {
            _weaponObjects = weapons ?? new List<I3dObject>();
            ParentShip = parent;
            ParentShipObject = ship;
        }

        public IWeapon FireWeapon(
            IVector3 trajectory,
            IVector3 startPosition,
            IVector3 worldPosition,
            WeaponType weaponType,
            I3dObject parentShip,
            int tilt
        )
        {
            ParentShipObject = (_3dObject)parentShip;
            var shipOffsets = ParentShipObject.ObjectOffsets ?? new Vector3(0, 0, 0);

            if (weaponType == WeaponType.Lazer)
            {
                I3dObject template = _weaponObjects.Count > 0
                    ? _weaponObjects[0]
                    : new _3dObject { ObjectName = "Lazer", ObjectId = GameState.ObjectIdCounter++ };

                I3dObject instance = Common3dObjectHelpers.DeepCopySingleObject(template);
                if (FireAsEnemyWeapon) instance.ObjectName = EnemyLazerName;
                instance.ImpactStatus = new ImpactStatus
                {
                    HasExploded = false,
                    HasCrashed = false,
                    ObjectName = "",
                    ImpactDirection = null,
                    SourceParticle = default,
                    ObjectHealth = 100
                };

                trajectory.z = -trajectory.z; // Invert Z to match WORLD coord system   

                IVector3 origDir = Normalize(trajectory);
                IVector3 dir = ApplyAimAssist(origDir, startPosition, weaponType);

                var (weaponRotation, weaponTilt) = AdjustWeaponRotation(
                    origDir, dir, ParentShipObject.Rotation, tilt);
                instance.Rotation = weaponRotation;

                // Exit point: 25% of the distance from start toward guide, offset by ship screen position
                var lazerStart = new Vector3
                {
                    x = startPosition.x + (trajectory.x - startPosition.x) * 0.25f + WeaponSetup.LazerExitOffsetX + shipOffsets.x,
                    y = startPosition.y + (trajectory.y - startPosition.y) * 0.25f + WeaponSetup.LazerExitOffsetY + shipOffsets.y,
                    z = startPosition.z + (trajectory.z - startPosition.z) * 0.25f + WeaponSetup.LazerExitOffsetZ + shipOffsets.z
                };
                SetObjectOffsets(instance, lazerStart);
                //Set world position for reference
                SetWorldPosition(instance, worldPosition);

                //Get ship rotation and apply to weapon geometry
                InitializeWeaponGeometry(instance, weaponRotation, weaponTilt);

                ActiveWeapon weapon = new ActiveWeapon
                {
                    WeaponType = weaponType,
                    WeaponObject = instance,
                    FiredTime = DateTime.UtcNow,
                    Velocity = LazerVelocityOverride ?? 3500f,
                    Acceleration = 0f,
                    Trajectory = dir,
                    MaxRange = LazerMaxRangeOverride ?? 6000f,
                    LifetimeSeconds = LazerLifetimeSecondsOverride ?? 3f,
                    DistanceTraveled = 0f,
                    LastUpdateUtc = default(DateTime)
                };

                ActiveWeapons.Add(weapon);

                if (_audio != null && _lazerSound != null)
                {
                    var audioPosition = ((_3dObject)parentShip).GetAudioPosition();
                    _lazerInstance = _audio.Play(
                        _lazerSound,
                        AudioPlayMode.OneShot,
                        new AudioPlayOptions
                        {
                            WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                        });
                }

                if (Logger.ShouldLog(enableLogging)) Logger.Log(
                    $"[WeaponSystem] Fired {weaponType} name={instance.ObjectName} parent={parentShip.ObjectName} " +
                    $"start=(x={startPosition.x:F2}; y={startPosition.y:F2}; z={startPosition.z:F2}) " +
                    $"dir=(x={dir.x:F4}; y={dir.y:F4}; z={dir.z:F4}) " +
                    $"spawnOffsets=(x={lazerStart.x:F2}; y={lazerStart.y:F2}; z={lazerStart.z:F2}) " +
                    $"parentOffsets=(x={shipOffsets.x:F2}; y={shipOffsets.y:F2}; z={shipOffsets.z:F2}) " +
                    $"worldPos=(x={worldPosition.x:F2}; y={worldPosition.y:F2}; z={worldPosition.z:F2}) " +
                    $"vel={weapon.Velocity:F1} range={weapon.MaxRange:F1} life={weapon.LifetimeSeconds:F2}s",
                    "LazerFire"
                );
            }

            if (weaponType == WeaponType.Bullet)
            {
                I3dObject? template = _weaponObjects.Find(w => w.ObjectName == "Bullet");
                if (template == null)
                    template = new _3dObject { ObjectName = "Bullet", ObjectId = GameState.ObjectIdCounter++ };

                I3dObject instance = Common3dObjectHelpers.DeepCopySingleObject(template);
                instance.ImpactStatus = new ImpactStatus
                {
                    HasExploded = false,
                    HasCrashed = false,
                    ObjectName = "",
                    ImpactDirection = null,
                    SourceParticle = default,
                    ObjectHealth = 100
                };

                trajectory.z = -trajectory.z;

                IVector3 origDir = Normalize(trajectory);
                IVector3 dir = ApplyAimAssist(origDir, startPosition, weaponType);

                var (weaponRotation, weaponTilt) = AdjustWeaponRotation(
                    origDir, dir, ParentShipObject.Rotation, tilt);
                instance.Rotation = weaponRotation;

                // Exit point: halfway between weapon start and guide, offset by ship screen position
                var bulletStart = new Vector3
                {
                    x = (startPosition.x + trajectory.x) * 0.5f + WeaponSetup.BulletExitOffsetX + shipOffsets.x,
                    y = (startPosition.y + trajectory.y) * 0.5f + WeaponSetup.BulletExitOffsetY + shipOffsets.y,
                    z = (startPosition.z + trajectory.z) * 0.5f + WeaponSetup.BulletExitOffsetZ + shipOffsets.z
                };
                SetObjectOffsets(instance, bulletStart);
                SetWorldPosition(instance, worldPosition);

                InitializeWeaponGeometry(instance, weaponRotation, weaponTilt);

                ActiveWeapon weapon = new ActiveWeapon
                {
                    WeaponType = weaponType,
                    WeaponObject = instance,
                    FiredTime = DateTime.UtcNow,
                    Velocity = 3000f,
                    Acceleration = 0f,
                    Trajectory = dir,
                    MaxRange = 4000f,
                    LifetimeSeconds = 2f,
                    DistanceTraveled = 0f,
                    LastUpdateUtc = default(DateTime)
                };

                ActiveWeapons.Add(weapon);

                if (Logger.ShouldLog(enableLogging)) Logger.Log(
                    $"[WeaponSystem] Fired {weaponType} from {startPosition} " +
                    $"with dir={dir} globalmapposition={worldPosition} at {DateTime.UtcNow}"
                );
            }

            return this;
        }

        private IVector3 ApplyAimAssist(IVector3 firingDir, IVector3 weaponStart, WeaponType weaponType)
        {
            float coneDot, strength, maxRange;
            switch (weaponType)
            {
                case WeaponType.Bullet:
                    coneDot  = WeaponSetup.BulletAimAssistConeDot;
                    strength = WeaponSetup.BulletAimAssistStrength;
                    maxRange = WeaponSetup.BulletAimAssistMaxRange;
                    break;
                case WeaponType.Rocket:
                    coneDot  = WeaponSetup.RocketAimAssistConeDot;
                    strength = WeaponSetup.RocketAimAssistStrength;
                    maxRange = WeaponSetup.RocketAimAssistMaxRange;
                    break;
                default: // Lazer and others
                    coneDot  = WeaponSetup.LazerAimAssistConeDot;
                    strength = WeaponSetup.LazerAimAssistStrength;
                    maxRange = WeaponSetup.LazerAimAssistMaxRange;
                    break;
            }

            if (strength <= 0f)
                return firingDir;

            var aiObjects = GameState.SurfaceState.AiObjects;
            if (aiObjects == null || aiObjects.Count == 0)
                return firingDir;

            float bestDot = coneDot;
            IVector3? bestEnemyDir = null;
            _3dObject? bestEnemy = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i] as _3dObject;
                if (!IsValidAimAssistEnemy(obj)) continue;
                if (!TryGetVisibleAimAssistTarget(obj, out var screenTarget)) continue;

                var shipOffsets = ParentShipObject?.ObjectOffsets ?? new Vector3(0, 0, 0);
                var toEnemy = new Vector3(
                    screenTarget.ScreenX - (ScreenSetup.screenSizeX / 2f) - shipOffsets.x - weaponStart.x,
                    screenTarget.ScreenY - (ScreenSetup.screenSizeY / 2f) - shipOffsets.y - weaponStart.y,
                    screenTarget.RenderZ - shipOffsets.z - weaponStart.z
                );

                // Enemy must be ahead of the ship (negative Y = forward on screen)
                if (toEnemy.y >= 0f) continue;

                float dist = Magnitude(toEnemy);
                if (dist < 1f || dist > maxRange) continue;

                IVector3 toEnemyDir = Normalize(toEnemy);
                float dot = firingDir.x * toEnemyDir.x + firingDir.y * toEnemyDir.y + firingDir.z * toEnemyDir.z;

                // Must be within the aim cone, then prefer closest
                if (dot > bestDot && dist < bestDist)
                {
                    bestDot = dot;
                    bestDist = dist;
                    bestEnemyDir = toEnemyDir;
                    bestEnemy = obj;
                }
            }

            _aimAssistLockedTarget = bestEnemy;

            if (bestEnemyDir == null)
                return firingDir;

            float keep = 1f - strength;
            return Normalize(new Vector3(
                firingDir.x * keep + bestEnemyDir.x * strength,
                firingDir.y * keep + bestEnemyDir.y * strength,
                firingDir.z * keep + bestEnemyDir.z * strength
            ));
        }

        private static (Vector3 rotation, int tilt) AdjustWeaponRotation(
            IVector3 origDir, IVector3 assistedDir, IVector3 shipRotation, int baseTilt)
        {
            // Weapon geometry points along -Y. InitializeWeaponGeometry applies:
            //   RotateX(baseTilt) → RotateZ(θ) → RotateX(cameraTilt)
            // After all rotations, the beam tip on screen is:
            //   Δx = cos(tilt)·sin(θ)
            //   Δy = -cos(tilt)·cos(θ)·cos(cT) + sin(tilt)·sin(cT)
            // Solving for θ that aligns the mesh with (dir.x, dir.y):
            //   θ = π − arcsin(tan(tilt)·sin(cT)·dir.x / R) − atan2(dir.x·cos(cT), dir.y)
            // where R = √(dir.y² + dir.x²·cos²(cT))
            float cameraTiltRad = shipRotation.x * (MathF.PI / 180f);
            float cosCT = MathF.Cos(cameraTiltRad);
            float sinCT = MathF.Sin(cameraTiltRad);

            float tiltRad = baseTilt * (MathF.PI / 180f);

            float dx = assistedDir.x;
            float dy = assistedDir.y;
            float R = MathF.Sqrt(dy * dy + dx * dx * cosCT * cosCT);

            float zRot;
            if (R < 1e-6f)
            {
                zRot = 0f;
            }
            else
            {
                float sinArg = MathF.Tan(tiltRad) * sinCT * dx / R;
                sinArg = MathF.Max(-1f, MathF.Min(1f, sinArg));
                zRot = (MathF.PI - MathF.Asin(sinArg) - MathF.Atan2(dx * cosCT, dy)) * (180f / MathF.PI);
            }

            var rotation = new Vector3(shipRotation.x, shipRotation.y, zRot);
            return (rotation, baseTilt);
        }

        //Rotates the weapon geometry according to ship rotation + tilt
        private void InitializeWeaponGeometry(I3dObject weaponObj, Vector3 rotation, int tilt)
        {
            if (weaponObj is not _3dObject weapon)
                return;

            if (weapon.ObjectParts == null)
                return;

            if (Logger.ShouldLog(enableLogging)) Logger.Log(
                $"Weapon rotation initialization started. BaseDeg:{rotation.x},{rotation.y},{rotation.z} Tilt:{tilt}"
            );
            if (Logger.ShouldLog(enableLogging)) Logger.Log("Before rotation:" +
                weapon.ObjectParts[0].Triangles[0].vert1.x + ", " +
                weapon.ObjectParts[0].Triangles[0].vert1.y + ", " +
                weapon.ObjectParts[0].Triangles[0].vert1.z);

            foreach (var part in weapon.ObjectParts)
            {
                if (part?.Triangles == null || part.Triangles.Count == 0)
                    continue;

                // 1) Tilt around X axis first
                var withTilt = _rotate.RotateXMesh(part.Triangles, tilt);

                // 2) Then the main rotation in the same order as in GameWorldManager.RotateMesh (Z → Y → X)
                var rotated = _rotate.RotateXMesh(
                    _rotate.RotateYMesh(
                        _rotate.RotateZMesh(withTilt, rotation.z),
                        rotation.y
                    ),
                    rotation.x
                );
                part.Triangles = rotated;
            }
            var crashBoxesWithTilt = RotateCrashBoxes(weapon.CrashBoxes, tilt, rotation);
            weapon.CrashBoxes = crashBoxesWithTilt;

            if (Logger.ShouldLog(enableLogging)) Logger.Log("After rotation:" +
                weapon.ObjectParts[0].Triangles[0].vert1.x + ", " +
                weapon.ObjectParts[0].Triangles[0].vert1.y + ", " +
                weapon.ObjectParts[0].Triangles[0].vert1.z);
        }

        public List<List<IVector3>>? RotateCrashBoxes(List<List<IVector3>> crashBoxes, int tilt, IVector3 rotation)
        {
            if (crashBoxes == null)
                return null;

            var result = new List<List<IVector3>>(crashBoxes.Count);

            foreach (var box in crashBoxes)
            {
                var rotatedBox = new List<IVector3>(box.Count);

                foreach (var vertex in box)
                {
                    // Start with original vertex
                    var v = new Vector3(vertex.x, vertex.y, vertex.z);

                    // 1) Tilt around X
                    v = (Vector3)_rotate.RotatePoint(tilt, v, 'X');

                    // 2) Main rotation Z → Y → X
                    v = (Vector3)_rotate.RotatePoint(rotation.z, v, 'Z');
                    v = (Vector3)_rotate.RotatePoint(rotation.y, v, 'Y');
                    v = (Vector3)_rotate.RotatePoint(rotation.x, v, 'X');

                    rotatedBox.Add(v);
                }

                result.Add(rotatedBox);
            }

            // Now all boxes have tilt + main rotation
            return result;
        }

        public IEnumerable<I3dObject> Get3DObjects()
        {
            if (ActiveWeapons.Count == 0) yield break;
            foreach (ActiveWeapon w in ActiveWeapons)
            {
                if (Expired(w))
                    continue;

                yield return w.WeaponObject;
            }
        }

        public void HandleHit(I3dObject weaponObject, bool hasCrashed, string objectName)
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log($"Weapon HasCrashed:{hasCrashed} ImpactName:{objectName}");
            if (_audio != null && _thudSound != null)
            {
                var audioPosition = ((_3dObject)weaponObject).GetAudioPosition();
                //Stop Lazer, it owerpowers the thud
                _lazerInstance?.Stop(playEndSegment: false);
                // Implement thudding sound or effects here
                _thudInstance = _audio.Play(
                    _thudSound,
                    AudioPlayMode.OneShot,
                    new AudioPlayOptions
                    {
                        WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                    });
            }
        }

        public void MoveWeapon(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (audioPlayer != null && soundRegistry != null) ConfigureAudio(audioPlayer, soundRegistry);

            if (ShowAimAssist) UpdateAimAssistTarget();

            DateTime now = DateTime.UtcNow;

            for (int i = 0; i < ActiveWeapons.Count; i++)
            {
                ActiveWeapon w = ActiveWeapons[i] as ActiveWeapon;

                //If weapon has crashed, handle hit effects
                if (w.WeaponObject.ImpactStatus.HasCrashed) HandleHit(w.WeaponObject, w.WeaponObject.ImpactStatus.HasCrashed, w.WeaponObject.ImpactStatus.ObjectName);

                if (w == null || Expired(w) || OutOfBounds(w.WeaponObject.ObjectOffsets) || w.WeaponObject.ImpactStatus.HasCrashed)
                    continue;

                double dt = w.LastUpdateUtc == default(DateTime)
                    ? 0.0
                    : (now - w.LastUpdateUtc).TotalSeconds;

                w.LastUpdateUtc = now;

                if (dt > 0.0)
                {
                    w.Velocity += w.Acceleration * (float)dt;
                    if (w.Velocity < 0f)
                        w.Velocity = 0f;

                    IVector3 deltaProj = Scale(w.Trajectory, w.Velocity * (float)dt);
                    IVector3 deltaParent = Scale(ParentVelocityLocal, (float)dt);
                    IVector3 local = GetObjectOffsets(w.WeaponObject);
                    IVector3 newLocal = Add(Add(local, deltaProj), deltaParent);

                    SetObjectOffsets(w.WeaponObject, newLocal);
                    w.DistanceTraveled += Magnitude(deltaProj);

                    if (Logger.ShouldLog(enableLogging)) Logger.Log(
                        $"[WeaponSystem] {w.WeaponType} name={w.WeaponObject.ObjectName} " +
                        $"Δ=(x={deltaProj.x:F2}; y={deltaProj.y:F2}; z={deltaProj.z:F2}) " +
                        $"LocalPos=(x={newLocal.x:F2}; y={newLocal.y:F2}; z={newLocal.z:F2}) " +
                        $"Range={w.DistanceTraveled:F2} vel={w.Velocity:F1} dt={dt:F4}",
                        "LazerMove"
                    );

                    ActiveWeapons[i] = w;
                }
            }

            for (int i = ActiveWeapons.Count - 1; i >= 0; i--)
            {
                ActiveWeapon w = ActiveWeapons[i] as ActiveWeapon;
                if (w != null && Expired(w) || OutOfBounds(w.WeaponObject.ObjectOffsets) || w.WeaponObject.ImpactStatus.HasCrashed)
                {
                    if (Logger.ShouldLog(enableLogging)) Logger.Log(
                        $"[WeaponSystem] {w.WeaponType} name={w.WeaponObject.ObjectName} expired/out/crashed " +
                        $"after {w.DistanceTraveled:F2} units. " +
                        $"LocalPos=(x={w.WeaponObject.ObjectOffsets.x:F2}; y={w.WeaponObject.ObjectOffsets.y:F2}; z={w.WeaponObject.ObjectOffsets.z:F2}) " +
                        $"hasCrashed={w.WeaponObject.ImpactStatus.HasCrashed}",
                        "LazerEnd"
                    );
                    ActiveWeapons.RemoveAt(i);
                }
            }
        }

        public List<List<IVector3>> GetCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            foreach (var w in ActiveWeapons)
            {
                if (w is not ActiveWeapon aw || Expired(aw))
                    continue;

                if (aw.WeaponObject?.CrashBoxes != null)
                    boxes.AddRange(aw.WeaponObject.CrashBoxes);
            }

            if (Logger.ShouldLog(enableLogging)) Logger.Log($"[WeaponSystem] CrashBoxes collected: {boxes.Count}");
            return boxes;
        }

        private void UpdateAimAssistTarget()
        {
            var gameplay = GameState.GamePlayState;
            gameplay.AimAssistTargetActive = false;

            // Only show when weapons are actively in flight
            if (ActiveWeapons.Count == 0)
            {
                _aimAssistLockedTarget = null;
                return;
            }

            var aiObjects = GameState.SurfaceState.AiObjects;
            if (aiObjects == null || aiObjects.Count == 0)
            {
                _aimAssistLockedTarget = null;
                return;
            }

            float halfW = ScreenSetup.screenSizeX / 2f;
            float halfH = ScreenSetup.screenSizeY / 2f;

            if (TrySetAimAssistTarget(gameplay, _aimAssistLockedTarget, halfW, halfH, out _))
                return;

            float bestDistSq = float.MaxValue;
            _3dObject? bestTarget = null;
            float bestSx = 0f, bestSy = 0f;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i] as _3dObject;
                if (!TrySetAimAssistTarget(null, obj, halfW, halfH, out var screenTarget)) continue;

                float dx = screenTarget.ScreenX - halfW;
                float dy = screenTarget.ScreenY - halfH;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTarget = obj;
                    bestSx = screenTarget.ScreenX;
                    bestSy = screenTarget.ScreenY;
                }
            }

            if (bestTarget != null)
            {
                _aimAssistLockedTarget = bestTarget;
                gameplay.AimAssistTargetActive = true;
                gameplay.AimAssistTargetScreenX = bestSx;
                gameplay.AimAssistTargetScreenY = bestSy;
            }
            else
            {
                _aimAssistLockedTarget = null;
            }
        }

        private readonly struct AimAssistScreenTarget
        {
            public AimAssistScreenTarget(float screenX, float screenY, float renderZ)
            {
                ScreenX = screenX;
                ScreenY = screenY;
                RenderZ = renderZ;
            }

            public float ScreenX { get; }
            public float ScreenY { get; }
            public float RenderZ { get; }
        }

        private static bool TrySetAimAssistTarget(
            GamePlayState? gameplay,
            _3dObject? obj,
            float halfW,
            float halfH,
            out AimAssistScreenTarget screenTarget)
        {
            screenTarget = default;

            if (!IsValidAimAssistEnemy(obj))
                return false;

            if (!TryGetVisibleAimAssistTarget(obj!, out screenTarget))
                return false;

            if (gameplay != null)
            {
                gameplay.AimAssistTargetActive = true;
                gameplay.AimAssistTargetScreenX = screenTarget.ScreenX;
                gameplay.AimAssistTargetScreenY = screenTarget.ScreenY;
            }

            return true;
        }

        private static bool IsValidAimAssistEnemy(_3dObject? obj)
        {
            if (obj == null) return false;
            if (!EnemySetup.IsEnemyTypeValid(obj.ObjectName)) return false;
            if (obj.ImpactStatus?.HasExploded == true) return false;
            if (obj.CrashBoxes == null || obj.CrashBoxes.Count == 0) return false;
            if (!obj.CheckInhabitantVisibility()) return false;
            return true;
        }

        private static bool TryGetVisibleAimAssistTarget(_3dObject obj, out AimAssistScreenTarget screenTarget)
        {
            screenTarget = default;

            if (!TryGetRenderOrigin(obj, out float originX, out float originY, out float originZ))
                return false;

            var localCenter = GetLocalCrashBoxCenter(obj);
            if (!TryProjectLocalPoint(localCenter, originX, originY, originZ, out float screenX, out float screenY))
                return false;

            if (screenX < 0f || screenX > ScreenSetup.screenSizeX) return false;
            if (screenY < 0f || screenY > ScreenSetup.screenSizeY) return false;

            screenTarget = new AimAssistScreenTarget(screenX, screenY, originZ + localCenter.z);
            return true;
        }

        private static bool TryGetRenderOrigin(_3dObject obj, out float x, out float y, out float z)
        {
            x = y = z = 0f;
            if (obj == null || obj.WorldPosition == null)
                return false;

            var objectOffsets = obj.ObjectOffsets ?? new Vector3(0, 0, 0);
            float halfW = ScreenSetup.screenSizeX / 2f;
            float halfH = ScreenSetup.screenSizeY / 2f;

            if (obj.WorldPosition.x == 0f && obj.WorldPosition.y == 0f && obj.WorldPosition.z == 0f)
            {
                x = halfW + objectOffsets.x;
                y = halfH + objectOffsets.y;
                z = objectOffsets.z;
                return true;
            }

            var globalMapPosition = GameState.SurfaceState.GlobalMapPosition;
            x = halfW - (globalMapPosition.x - obj.WorldPosition.x) + objectOffsets.x;
            y = halfH - (globalMapPosition.y - obj.WorldPosition.y) + objectOffsets.y;
            z = (globalMapPosition.z - obj.WorldPosition.z) + objectOffsets.z;
            return true;
        }

        private static Vector3 GetLocalCrashBoxCenter(_3dObject obj)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            bool hasPoint = false;

            foreach (var box in obj.CrashBoxes)
            {
                if (box == null) continue;

                foreach (var point in box)
                {
                    if (point == null) continue;

                    hasPoint = true;
                    if (point.x < minX) minX = point.x;
                    if (point.x > maxX) maxX = point.x;
                    if (point.y < minY) minY = point.y;
                    if (point.y > maxY) maxY = point.y;
                    if (point.z < minZ) minZ = point.z;
                    if (point.z > maxZ) maxZ = point.z;
                }
            }

            if (!hasPoint)
                return new Vector3();

            return new Vector3
            {
                x = (minX + maxX) / 2f,
                y = (minY + maxY) / 2f,
                z = (minZ + maxZ) / 2f
            };
        }

        private static bool TryProjectLocalPoint(
            Vector3 localPoint,
            float originX,
            float originY,
            float originZ,
            out float screenX,
            out float screenY)
        {
            screenX = 0f;
            screenY = 0f;

            double denom = -localPoint.z + originZ + ScreenSetup.perspectiveAdjustment;
            if (denom <= 1.0)
                return false;

            double factor = ScreenSetup.perspectiveAdjustment / denom;
            screenX = (float)(localPoint.x * factor * ScreenSetup.defaultObjectZoom + originX);
            screenY = (float)(localPoint.y * factor * ScreenSetup.defaultObjectZoom + originY);
            return !float.IsNaN(screenX) && !float.IsNaN(screenY);
        }

        private static void SetWorldPosition(I3dObject obj, IVector3 pos)
        {
            var effectivePos = pos.x + obj.ObjectOffsets?.x ?? 0;
            effectivePos = pos.y + obj.ObjectOffsets?.y ?? 0;
            effectivePos = pos.z + obj.ObjectOffsets?.z ?? 0;
            ((_3dObject)obj).WorldPosition = pos;
        }

        private static void SetObjectOffsets(I3dObject obj, IVector3 pos)
        {
            ((_3dObject)obj).ObjectOffsets = pos;
        }

        private static IVector3 GetObjectOffsets(I3dObject obj)
        {
            var o = (_3dObject)obj;
            return o.ObjectOffsets ?? new Vector3(0, 0, 0);
        }

        private static bool Expired(ActiveWeapon w)
        {
            double lifetime = (DateTime.UtcNow - w.FiredTime).TotalSeconds;
            return lifetime > w.LifetimeSeconds || w.DistanceTraveled >= w.MaxRange;
        }

        private static bool OutOfBounds(IVector3 pos)
        {
            return pos.z > maxZ || pos.z < minZ || pos.x < minX || pos.x > maxX || pos.y < minY || pos.y > maxY;
        }

        private static IVector3 Add(IVector3 a, IVector3 b) =>
            new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);

        private static IVector3 Scale(IVector3 a, float s) =>
            new Vector3(a.x * s, a.y * s, a.z * s);

        private static float Magnitude(IVector3 a) =>
            (float)Math.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z);

        private static IVector3 Normalize(IVector3 a)
        {
            float m = Magnitude(a);
            if (m <= 1e-6f)
                return new Vector3(0, 0, 0);

            float inv = 1f / m;
            return new Vector3(a.x * inv, a.y * inv, a.z * inv);
        }
    }
}
