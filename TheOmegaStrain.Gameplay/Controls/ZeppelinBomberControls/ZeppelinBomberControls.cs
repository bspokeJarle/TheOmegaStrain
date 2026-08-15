using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;
using System;
using System.Collections.Generic;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Gameplay.Controls.ZeppelinBomberControls
{
    public class ZeppelinBomberControls : IObjectMovement
    {
        private const float BaseXRotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private const float BaseYRotation = 0f;
        private const float BaseZRotation = 90f;
        private const float RotationDegreesPerSecond = 60f;
        private const float ExplosionForce = 200f;
        private const float SurfaceAltitudeLift = 60f;

        // Propeller animation
        private const float PropellerDegreesPerSecond = 720f;
        private float _propellerRotation = 0f;
        private List<ITriangleMeshWithColor>? _propellerOriginalTriangles;
        private Vector3? _propellerPivot;

        // Bomb bay hatch animation
        private const float HatchMaxAngle = 55f;
        private const float HatchOpenDegreesPerSecond = 90f;
        private const float HatchCloseDegreesPerSecond = 60f;
        private const float HatchHoldSeconds = 1.5f;

        private enum HatchState { Closed, Opening, Open, Closing }
        private HatchState _hatchState = HatchState.Closed;
        private float _hatchAngle = 0f;
        private float _hatchHoldTimer = 0f;

        // Bomb spawning
        private ITriangleMeshWithColor? _bombDropStartGuide;
        private ITriangleMeshWithColor? _bombDropEndGuide;
        private bool _bombSpawnedThisCycle = false;

        // Surface sync
        private bool _syncInitialized = false;
        private float _syncY = 0f;

        // Timing
        private DateTime _lastFrameTime = DateTime.MinValue;

        // Heading rotation
        private float Xrotation = BaseXRotation;
        private float Yrotation = BaseYRotation;
        private float Zrotation = BaseZRotation;
        private float TargetXrotation = BaseXRotation;
        private float TargetYrotation = BaseYRotation;
        private float TargetZrotation = BaseZRotation;

        // AI state
        private readonly ZeppelinBomberAi.BomberState _aiState = new();
        private Vector3? _trackedWorldPosition;

        // Explosion state
        private bool _isExploding = false;
        private DateTime _explosionDeltaTime;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;

        // Audio
        private bool _audioConfigured = false;
        private IAudioPlayer? _audio;
        private SoundDefinition? _propellerSound;
        private IAudioInstance? _propellerInstance;
        private SoundDefinition? _explosionSound;

        private readonly OmegaMeshRotation _rotate = new();

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ConfigureAudio(audioPlayer, soundRegistry);
            ParentObject = theObject;

            if (theObject.ImpactStatus?.HasExploded == true)
                return theObject;

            ZeppelinBomberAi.Initialize(_aiState);

            // Initialize tracked position from the object on first call
            if (_trackedWorldPosition == null)
            {
                _trackedWorldPosition = new Vector3
                {
                    x = theObject.WorldPosition.x,
                    y = theObject.WorldPosition.y,
                    z = theObject.WorldPosition.z
                };
            }

            float deltaSeconds = 0f;
            if (_lastFrameTime != DateTime.MinValue)
            {
                deltaSeconds = (float)(DateTime.Now - _lastFrameTime).TotalSeconds;
                deltaSeconds = Math.Clamp(deltaSeconds, 0f, 0.1f);
            }
            _lastFrameTime = DateTime.Now;

            TerrainAvoidanceHelpers.TryStartTerrainRecovery(theObject);

            // AI movement
            if (!_isExploding && theObject.ImpactStatus?.HasCrashed != true)
            {
                ZeppelinBomberAi.UpdateMovement(_aiState, _trackedWorldPosition, deltaSeconds);
            }

            // Apply tracked position
            theObject.WorldPosition = new Vector3
            {
                x = _trackedWorldPosition.x,
                y = _trackedWorldPosition.y,
                z = _trackedWorldPosition.z
            };

            // Handle crash / health depletion
            if (theObject.ImpactStatus?.HasCrashed == true && !_isExploding)
            {
                HandleCrash(theObject);
            }

            // Explosion
            if (_isExploding)
            {
                if (_explosionWorldPosition != null)
                    theObject.WorldPosition = new Vector3 { x = _explosionWorldPosition.x, y = _explosionWorldPosition.y, z = _explosionWorldPosition.z };
                if (_explosionObjectOffsets != null)
                    theObject.ObjectOffsets = new Vector3 { x = _explosionObjectOffsets.x, y = _explosionObjectOffsets.y, z = _explosionObjectOffsets.z };

                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                ExplosionParticleHelpers.MoveParticles(theObject);
                if (theObject.ImpactStatus?.HasExploded == true)
                {
                    theObject.ObjectParts = new List<I3dObjectPart>();
                }

                SyncToOriginal(theObject);
                return theObject;
            }

            // Heading rotation from AI direction
            var heading = OmegaObjectHelpers.GetHeadingFromDirection(_aiState.DirectionX, _aiState.DirectionZ);
            TargetXrotation = heading.X;
            TargetYrotation = heading.Y;
            TargetZrotation = heading.Z;

            float maxDelta = RotationDegreesPerSecond * deltaSeconds;
            if (maxDelta > 0f)
            {
                Xrotation = OmegaObjectHelpers.MoveAngleTowards(Xrotation, TargetXrotation, maxDelta);
                Yrotation = OmegaObjectHelpers.MoveAngleTowards(Yrotation, TargetYrotation, maxDelta);
                Zrotation = OmegaObjectHelpers.MoveAngleTowards(Zrotation, TargetZrotation, maxDelta);
            }

            if (theObject.Rotation != null)
            {
                theObject.Rotation.x = Xrotation;
                theObject.Rotation.y = Yrotation;
                theObject.Rotation.z = Zrotation;
            }

            SyncMovement(theObject);
            if (TerrainAvoidanceHelpers.ApplyTerrainRecovery(theObject, deltaSeconds) && theObject.WorldPosition != null)
            {
                _trackedWorldPosition = new Vector3
                {
                    x = theObject.WorldPosition.x,
                    y = theObject.WorldPosition.y,
                    z = theObject.WorldPosition.z
                };
            }

            AnimatePropeller(deltaSeconds);
            AnimateHatch(deltaSeconds);
            HitSparkEffects.MoveHitSparks(theObject);

            UpdatePropellerAudio(theObject);

            // AI-driven bomb dropping
            if (ZeppelinBomberAi.ShouldDropBomb(_aiState, deltaSeconds) && _hatchState == HatchState.Closed)
            {
                OpenBombBay();
            }

            SyncToOriginal(theObject);
            return theObject;
        }

        private void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null) return;

            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? EnemySetup.ZeppelinBomberHealth;
            int damage = theObject.ImpactStatus.ObjectName switch
            {
                "Ship" => currentHealth,
                string name when WeaponSetup.IsWeaponTypeValid(name) => WeaponSetup.GetWeaponDamage(name),
                _ => currentHealth
            };

            theObject.ImpactStatus.ObjectHealth = currentHealth - damage;

            if (theObject.ImpactStatus.ObjectHealth > 0)
            {
                HitSparkEffects.ReleaseHitSparks(theObject, this, theObject.ImpactStatus.ObjectName);
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

            // Release tracker slot when destroyed
            ZeppelinBomberAi.ReleaseTracker(_aiState);

            // Play explosion sound
            if (_audio != null && _explosionSound != null)
            {
                var audioPosition = ((_3dObject)theObject).GetAudioPosition();
                _audio.Play(_explosionSound, AudioPlayMode.OneShot, new AudioPlayOptions
                {
                    WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                });
            }

            // Stop propeller audio
            if (_propellerInstance?.IsPlaying == true)
                _propellerInstance.Stop(false);

            _isExploding = true;
            _explosionDeltaTime = DateTime.Now;
            _explosionWorldPosition = new Vector3
            {
                x = theObject.WorldPosition?.x ?? 0f,
                y = theObject.WorldPosition?.y ?? 0f,
                z = theObject.WorldPosition?.z ?? 0f
            };
            _explosionObjectOffsets = new Vector3
            {
                x = theObject.ObjectOffsets?.x ?? 0f,
                y = theObject.ObjectOffsets?.y ?? 0f,
                z = theObject.ObjectOffsets?.z ?? 0f
            };

            ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
            Physics.ExplodeObject(theObject, ExplosionForce);
            theObject.CrashBoxes = new List<List<IVector3>>();
        }

        public void OpenBombBay()
        {
            if (_hatchState == HatchState.Closed)
            {
                _hatchState = HatchState.Opening;
            }
        }

        private void AnimatePropeller(float deltaSeconds)
        {
            if (ParentObject == null) return;

            var propPart = ParentObject.ObjectParts.Find(p => p.PartName == "BomberPropeller");
            if (propPart?.Triangles == null || propPart.Triangles.Count == 0) return;

            _propellerRotation = NormalizeAngle(_propellerRotation + PropellerDegreesPerSecond * deltaSeconds);

            InitializePropellerAnimation(propPart);
            if (_propellerOriginalTriangles == null || _propellerPivot == null) return;

            var center = _propellerPivot;
            var atOrigin = TranslateMesh(_propellerOriginalTriangles, new Vector3 { x = -center.x, y = -center.y, z = -center.z });
            var rotated = _rotate.RotateXMesh(atOrigin, _propellerRotation);
            propPart.Triangles = TranslateMesh(rotated, center);
        }

        private void InitializePropellerAnimation(I3dObjectPart propPart)
        {
            if (_propellerOriginalTriangles != null && _propellerPivot != null) return;

            _propellerOriginalTriangles = TranslateMesh(propPart.Triangles, new Vector3());
            _propellerPivot = GetFrontFaceCenter(propPart.Triangles);
        }

        private void AnimateHatch(float deltaSeconds)
        {
            if (ParentObject == null) return;

            switch (_hatchState)
            {
                case HatchState.Opening:
                    _hatchAngle += HatchOpenDegreesPerSecond * deltaSeconds;
                    if (_hatchAngle >= HatchMaxAngle)
                    {
                        _hatchAngle = HatchMaxAngle;
                        _hatchState = HatchState.Open;
                        _hatchHoldTimer = 0f;
                        if (!_bombSpawnedThisCycle)
                        {
                            _bombSpawnedThisCycle = true;
                            SpawnBomb();
                        }
                    }
                    break;

                case HatchState.Open:
                    _hatchHoldTimer += deltaSeconds;
                    if (_hatchHoldTimer >= HatchHoldSeconds)
                    {
                        _hatchState = HatchState.Closing;
                    }
                    break;

                case HatchState.Closing:
                    _hatchAngle -= HatchCloseDegreesPerSecond * deltaSeconds;
                    if (_hatchAngle <= 0f)
                    {
                        _hatchAngle = 0f;
                        _hatchState = HatchState.Closed;
                        _bombSpawnedThisCycle = false;
                    }
                    break;

                case HatchState.Closed:
                    return;
            }

            var bayPart = ParentObject.ObjectParts.Find(p => p.PartName == "BomberBombBay");
            if (bayPart?.Triangles == null || bayPart.Triangles.Count == 0) return;

            var center = GetPartCenter(bayPart.Triangles);
            var atOrigin = TranslateMesh(bayPart.Triangles, new Vector3 { x = -center.x, y = -center.y, z = -center.z });
            var rotated = _rotate.RotateXMesh(atOrigin, _hatchAngle);
            bayPart.Triangles = TranslateMesh(rotated, center);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        private static Vector3 GetPartCenter(List<ITriangleMeshWithColor> triangles)
        {
            if (triangles == null || triangles.Count == 0)
                return new Vector3();

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var triangle in triangles)
            {
                var vertices = new[] { (Vector3)triangle.vert1, (Vector3)triangle.vert2, (Vector3)triangle.vert3 };
                foreach (var vertex in vertices)
                {
                    if (vertex.x < minX) minX = vertex.x;
                    if (vertex.y < minY) minY = vertex.y;
                    if (vertex.z < minZ) minZ = vertex.z;
                    if (vertex.x > maxX) maxX = vertex.x;
                    if (vertex.y > maxY) maxY = vertex.y;
                    if (vertex.z > maxZ) maxZ = vertex.z;
                }
            }

            return new Vector3
            {
                x = (minX + maxX) * 0.5f,
                y = (minY + maxY) * 0.5f,
                z = (minZ + maxZ) * 0.5f
            };
        }

        private static Vector3 GetFrontFaceCenter(List<ITriangleMeshWithColor> triangles)
        {
            if (triangles == null || triangles.Count == 0)
                return new Vector3();

            float maxX = float.MinValue;
            foreach (var triangle in triangles)
            {
                if (triangle.vert1.x > maxX) maxX = triangle.vert1.x;
                if (triangle.vert2.x > maxX) maxX = triangle.vert2.x;
                if (triangle.vert3.x > maxX) maxX = triangle.vert3.x;
            }

            const float tolerance = 0.001f;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            int count = 0;

            foreach (var triangle in triangles)
            {
                MeasureFrontVertex(triangle.vert1);
                MeasureFrontVertex(triangle.vert2);
                MeasureFrontVertex(triangle.vert3);
            }

            if (count == 0)
                return GetPartCenter(triangles);

            return new Vector3
            {
                x = maxX,
                y = (minY + maxY) * 0.5f,
                z = (minZ + maxZ) * 0.5f
            };

            void MeasureFrontVertex(IVector3 vertex)
            {
                if (MathF.Abs(vertex.x - maxX) > tolerance) return;

                if (vertex.y < minY) minY = vertex.y;
                if (vertex.y > maxY) maxY = vertex.y;
                if (vertex.z < minZ) minZ = vertex.z;
                if (vertex.z > maxZ) maxZ = vertex.z;
                count++;
            }
        }

        private static List<ITriangleMeshWithColor> TranslateMesh(List<ITriangleMeshWithColor> triangles, Vector3 offset)
        {
            var translated = new List<ITriangleMeshWithColor>(triangles.Count);

            foreach (var triangle in triangles)
            {
                translated.Add(new TriangleMeshWithColor
                {
                    Color = triangle.Color,
                    noHidden = triangle.noHidden,
                    landBasedPosition = triangle.landBasedPosition,
                    angle = triangle.angle,
                    vert1 = new Vector3 { x = triangle.vert1.x + offset.x, y = triangle.vert1.y + offset.y, z = triangle.vert1.z + offset.z },
                    vert2 = new Vector3 { x = triangle.vert2.x + offset.x, y = triangle.vert2.y + offset.y, z = triangle.vert2.z + offset.z },
                    vert3 = new Vector3 { x = triangle.vert3.x + offset.x, y = triangle.vert3.y + offset.y, z = triangle.vert3.z + offset.z },
                    normal1 = new Vector3 { x = triangle.normal1.x, y = triangle.normal1.y, z = triangle.normal1.z },
                    normal2 = new Vector3 { x = triangle.normal2.x, y = triangle.normal2.y, z = triangle.normal2.z },
                    normal3 = new Vector3 { x = triangle.normal3.x, y = triangle.normal3.y, z = triangle.normal3.z }
                });
            }

            return translated;
        }

        private void SyncMovement(I3dObject theObject)
        {
            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = (theObject.ObjectOffsets?.y ?? 0f) - SurfaceAltitudeLift;
            }

            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(theObject, _syncY);
        }

        private static void SyncToOriginal(I3dObject source)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null) return;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectId == source.ObjectId)
                {
                    var target = aiObjects[i];
                    if (ReferenceEquals(target, source)) return;

                    target.WorldPosition = new Vector3
                    {
                        x = source.WorldPosition?.x ?? 0f,
                        y = source.WorldPosition?.y ?? 0f,
                        z = source.WorldPosition?.z ?? 0f
                    };
                    target.ObjectOffsets = new Vector3
                    {
                        x = source.ObjectOffsets?.x ?? 0f,
                        y = source.ObjectOffsets?.y ?? 0f,
                        z = source.ObjectOffsets?.z ?? 0f
                    };
                    return;
                }
            }
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured) return;
            if (audioPlayer == null || soundRegistry == null) return;

            _audio = audioPlayer;
            _explosionSound = soundRegistry.Get("explosion_main");

            if (soundRegistry.TryGet("zeppelin_propeller", out var propSound))
                _propellerSound = propSound;

            _audioConfigured = true;
        }

        private void UpdatePropellerAudio(I3dObject theObject)
        {
            if (_audio == null || _propellerSound == null) return;

            if (theObject.IsOnScreen)
            {
                var audioPosition = ((_3dObject)theObject).GetAudioPosition();

                if (_propellerInstance == null || !_propellerInstance.IsPlaying)
                {
                    _propellerInstance = _audio.Play(
                        _propellerSound,
                        AudioPlayMode.SegmentedLoop,
                        new AudioPlayOptions
                        {
                            WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                        });
                }

                _propellerInstance.SetVolume(_propellerSound.Settings.Volume);
                _propellerInstance.SetWorldPosition(new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z));
            }
            else
            {
                var globalPos = GameState.SurfaceState?.GlobalMapPosition;
                var bomberWorldPos = theObject.WorldPosition;

                if (globalPos != null && bomberWorldPos != null)
                {
                    float distSq = OmegaObjectHelpers.GetDistanceSquared(globalPos, bomberWorldPos);
                    float maxDist = AudioSetup.OffscreenAiAudioMaxDistance;
                    float maxDistSq = maxDist * maxDist;

                    if (distSq <= maxDistSq)
                    {
                        float distance = MathF.Sqrt(distSq);
                        float normalized = distance / maxDist;
                        float volume = _propellerSound.Settings.Volume *
                            MathF.Pow(1f - normalized, AudioSetup.OffscreenAiAudioCurveExponent);

                        if (_propellerInstance == null || !_propellerInstance.IsPlaying)
                        {
                            _propellerInstance = _audio.Play(
                                _propellerSound,
                                AudioPlayMode.SegmentedLoop,
                                new AudioPlayOptions
                                {
                                    WorldPosition = System.Numerics.Vector3.Zero
                                });
                        }

                        float dx = bomberWorldPos.x - globalPos.x;
                        _propellerInstance.SetWorldPosition(new System.Numerics.Vector3(dx, 0, 0));
                        _propellerInstance.SetVolume(volume);
                    }
                    else if (_propellerInstance != null)
                    {
                        _propellerInstance.Stop(playEndSegment: false);
                        _propellerInstance = null;
                    }
                }
                else if (_propellerInstance != null)
                {
                    _propellerInstance.Stop(playEndSegment: false);
                    _propellerInstance = null;
                }
            }
        }

        public void ReleaseParticles(I3dObject theObject) { }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) _bombDropStartGuide = StartCoord;
            if (GuideCoord != null) _bombDropEndGuide = GuideCoord;
        }

        private void SpawnBomb()
        {
            if (ParentObject?.ParentSurface == null) return;

            var bomb = CreateBomberBombObject(ParentObject.ParentSurface);
            if (bomb == null) return;

            var globalMapY = GameState.SurfaceState?.GlobalMapPosition?.y ?? 0f;
            float unsyncedY = (ParentObject.ObjectOffsets?.y ?? 0f) - globalMapY * SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY;

            bomb.WorldPosition = new Vector3
            {
                x = ParentObject.WorldPosition?.x ?? 0f,
                y = ParentObject.WorldPosition?.y ?? 0f,
                z = ParentObject.WorldPosition?.z ?? 0f
            };
            bomb.ObjectOffsets = new Vector3
            {
                x = ParentObject.ObjectOffsets?.x ?? 0f,
                y = unsyncedY,
                z = ParentObject.ObjectOffsets?.z ?? 0f
            };
            bomb.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            bomb.ObjectName = "BomberBomb";
            bomb.ImpactStatus = new ImpactStatus { ObjectHealth = 30 };
            bomb.CrashBoxDebugMode = false;
            bomb.Movement = new BomberBombControls();
            bomb.IsActive = true;

            GameState.SurfaceState.AiObjects.Add(bomb);
            GameState.PendingWorldObjects.Add(bomb);
        }

        private static _3dObject? CreateBomberBombObject(ISurface parentSurface)
        {
            const string typeName = "TheOmegaStrain.Game.World.Objects.BomberBomb";
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (type == null) continue;
                var method = type.GetMethod("CreateBomberBomb", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method?.Invoke(null, new object[] { parentSurface }) is _3dObject bomb)
                {
                    return bomb;
                }
            }
            return null;
        }

        public void Dispose()
        {
            _propellerRotation = 0f;
            _propellerOriginalTriangles = null;
            _propellerPivot = null;
            _hatchAngle = 0f;
            _hatchState = HatchState.Closed;
            _hatchHoldTimer = 0f;
            _bombSpawnedThisCycle = false;
            _syncInitialized = false;
            _syncY = 0f;
            _lastFrameTime = DateTime.MinValue;
            _trackedWorldPosition = null;
            _isExploding = false;
            _explosionWorldPosition = null;
            _explosionObjectOffsets = null;
            StartCoordinates = null;
            GuideCoordinates = null;
            _bombDropStartGuide = null;
            _bombDropEndGuide = null;

            ZeppelinBomberAi.ResetState(_aiState);

            if (_propellerInstance?.IsPlaying == true)
                _propellerInstance.Stop(false);
            _propellerInstance = null;
            _propellerSound = null;
            _explosionSound = null;
            _audio = null;
            _audioConfigured = false;
        }
    }
}
