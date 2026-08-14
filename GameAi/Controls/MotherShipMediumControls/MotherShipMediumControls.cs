using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using static Domain.WeaponHelpers;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.MotherShipMediumControls
{
    public class MotherShipMediumControls : IObjectMovement
    {
        // -------------------------------------------------------
        //  Interface properties
        // -------------------------------------------------------
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        // -------------------------------------------------------
        //  Rotation
        // -------------------------------------------------------
        private const float BaseXRotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private const float BaseYRotation = 0f;
        private const float BaseZRotation = 90f;
        private const float RotationDegreesPerSecond = 15f;
        private const float DirectionUpdateIntervalSeconds = 2.0f;
        private const float SpinSpeed = 10f;

        private float Xrotation = BaseXRotation;
        private float Yrotation = BaseYRotation;
        private float Zrotation = BaseZRotation;
        private float TargetXrotation = BaseXRotation;
        private float TargetYrotation = BaseYRotation;
        private float TargetZrotation = BaseZRotation;
        private DateTime _lastDirectionUpdateTime = DateTime.MinValue;
        private DateTime _lastMovementTime = DateTime.MinValue;

        // -------------------------------------------------------
        //  Descent
        // -------------------------------------------------------
        private const float DescentDurationSeconds = 4.0f;
        private const float DescentStartOffsetY = -1500f;
        private const float DescentTargetOffsetY = -150f;
        private const float DescentSpawnOffsetZ = -1500f;

        private bool _descentInitialized = false;
        private bool _isDescending = true;
        private float _descentStartY = DescentStartOffsetY;
        private DateTime _descentStartTime = DateTime.MinValue;

        // -------------------------------------------------------
        //  Wing-engine particle guides (left & right)
        // -------------------------------------------------------
        private ITriangleMeshWithColor? _leftEngineStart;
        private ITriangleMeshWithColor? _leftEngineGuide;
        private ITriangleMeshWithColor? _rightEngineStart;
        private ITriangleMeshWithColor? _rightEngineGuide;

        // -------------------------------------------------------
        //  Weapon guides (from rotated WeaponStartGuide / WeaponDirectionGuide parts)
        // -------------------------------------------------------
        private ITriangleMeshWithColor? _weaponStartGuide;
        private ITriangleMeshWithColor? _weaponDirectionGuide;

        // Local logging gate — paired with global Logger.EnableFileLogging. Leave false by default
        // to avoid noise; flip to true when debugging the guide/fire pipeline.
        private const bool enableLogging = false;

        // -------------------------------------------------------
        //  Wing-engine rotation animation
        //  Rotates each nozzle around the X axis with the pivot at the SHIP CENTERLINE
        //  (y = 0). Since the nozzles sit at y = ±90, they swing in a big visible arc.
        //  Now configured as a continuous 360° rotation (not an oscillation) so the
        //  motion is always clearly visible.
        // -------------------------------------------------------
        private const float EngineTiltSpeed = 90f;        // degrees per second (unused for oscillation; kept for future use)
        private const float EngineRotationSpeed = 120f;   // degrees per second (legacy; unused after gimbal change)
        private const float MaxGimbalDegrees = 35f;       // pod pitch gimbal half-range
        private const float EngineGimbalSpeed = 90f;      // degrees of phase per second for the sine oscillation
        private float _engineTiltAngle = 0f;              // current animated rotation angle (0..360)
        private float _prevMovementSpeed = 0f;
        private readonly OmegaMeshRotation _rotate = new();

        // Original (unrotated) triangle baselines for engine + pod parts — captured once on first AnimateEngines call.
        // The whole pod assembly (pod housing, connector, nacelle vents, engine + engine guide) rotates together
        // around the ship centerline so the engine pods visibly swing as one unit.
        private List<ITriangleMeshWithColor>? _leftEngineOriginalTris;
        private List<ITriangleMeshWithColor>? _rightEngineOriginalTris;
        private List<ITriangleMeshWithColor>? _leftEngineGuideOriginalTris;
        private List<ITriangleMeshWithColor>? _rightEngineGuideOriginalTris;
        private List<ITriangleMeshWithColor>? _leftEngineStartOriginalTris;
        private List<ITriangleMeshWithColor>? _rightEngineStartOriginalTris;
        private List<ITriangleMeshWithColor>? _leftPodOriginalTris;
        private List<ITriangleMeshWithColor>? _rightPodOriginalTris;
        private List<ITriangleMeshWithColor>? _leftConnectorOriginalTris;
        private List<ITriangleMeshWithColor>? _rightConnectorOriginalTris;
        private List<ITriangleMeshWithColor>? _podNacelleVentsOriginalTris;

        // -------------------------------------------------------
        //  Cannon muzzle ball spin animation
        //  The glowing cone in front of the cannon (FrontCannonMuzzle) spins around
        //  the barrel axis (X). Alternating ring colors (cannonGlow / cannonGlowBright)
        //  make the rotation visually obvious.
        // -------------------------------------------------------
        private const float MuzzleSpinSpeed = 180f;     // degrees per second
        private float _muzzleSpinAngle = 0f;
        private List<ITriangleMeshWithColor>? _muzzleOriginalTris;

        // -------------------------------------------------------
        //  Engine color animation (idle dark-red → active yellow)
        // -------------------------------------------------------
        private static readonly (int r, int g, int b) EngineActiveRgb = (0xFF, 0xFF, 0x00);
        private static readonly (int r, int g, int b) EngineIdleRgb   = (0x88, 0x11, 0x00);
        private const float EngineFlickerAmount = 0.10f;
        private static readonly Random _flickerRng = new();
        private static readonly Random _emitterRng = new();

        // -------------------------------------------------------
        //  Ship-follow
        // -------------------------------------------------------
        private const float WorldTravelSpeed = 120f;      // world units per second
        private const float StandoffDistance = 800f;       // keep this far from the ship
        private float _travelTargetX = float.NaN;
        private float _travelTargetZ = float.NaN;

        // -------------------------------------------------------
        //  Sync
        // -------------------------------------------------------
        private const float SyncFactorY = 2.5f;
        private const float SyncAnchorY = -105f;
        private bool _syncInitialized = false;
        private float _syncY = SyncAnchorY;

        // -------------------------------------------------------
        //  Weapon / charge
        // -------------------------------------------------------
        private const float ChargeWindowSeconds = 2.0f;
        private const float FireIntervalSeconds = 5.0f;
        private float _fireTimer = 0f;
        private bool _chargeSoundPlayed = false;
        private IAudioInstance? _chargeInstance;

        // -------------------------------------------------------
        //  Audio
        // -------------------------------------------------------
        private bool _audioConfigured = false;
        private IAudioPlayer? _audio;
        private SoundDefinition? _chargeSound;
        private SoundDefinition? _lazerFireSound;
        private SoundDefinition? _engineSound;
        private IAudioInstance? _engineInstance;
        private SoundDefinition? _thudSound;
        private SoundDefinition? _explosionSound;
        private SoundDefinition? _imminentSound;

        // -------------------------------------------------------
        //  Explosion / flash
        // -------------------------------------------------------
        private bool _isExploding = false;
        private bool _secondExplosionTriggered = false;
        private DateTime _explosionDeltaTime = DateTime.Now;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;
        private const float FirstExplosionForce = 3f;
        private const float SecondExplosionForce = 5f;
        private const float SecondExplosionDelaySeconds = 1.2f;
        private const string FlashColor = "#FF4444";
        private const float FlashDurationSeconds = 0.12f;
        private DateTime _flashStartTime = DateTime.MinValue;
        private bool _isFlashing = false;
        private List<List<string?>>? _originalColors;
        private DateTime _shipCollisionCooldown = DateTime.MinValue;
        private const int HullHitsToDestroy = 20;
        private const float ShipRamDamagePercent = 0.05f;

        // -------------------------------------------------------
        //  MoveObject
        // -------------------------------------------------------
        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ConfigureAudio(audioPlayer, soundRegistry);
            ParentObject = theObject;



            if (theObject.ImpactStatus?.HasExploded == true)
                return theObject;

            TerrainAvoidanceHelpers.TryStartTerrainRecovery(theObject);

            if (theObject.ImpactStatus?.HasCrashed == true && !_isExploding)
                HandleCrash(theObject);

            if (_isExploding)
            {
                if (_explosionWorldPosition != null) theObject.WorldPosition = _explosionWorldPosition;
                if (_explosionObjectOffsets != null) theObject.ObjectOffsets = _explosionObjectOffsets;

                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                ExplosionParticleHelpers.MoveParticles(theObject);

                if (!_secondExplosionTriggered &&
                    (DateTime.Now - _explosionDeltaTime).TotalSeconds >= SecondExplosionDelaySeconds)
                {
                    _secondExplosionTriggered = true;
                    PlayExplosionSound(theObject);
                    ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
                    Physics.ExplodeObject(theObject, SecondExplosionForce);
                    _explosionDeltaTime = DateTime.Now;
                }

                if (theObject.ImpactStatus?.HasExploded == true)
                    theObject.ObjectParts = new List<I3dObjectPart>();

                SyncToOriginal(theObject);
                _lastMovementTime = DateTime.Now;
                return theObject;
            }

            var now = DateTime.Now;
            if (_lastMovementTime == DateTime.MinValue)
                _lastMovementTime = now;

            double deltaSeconds = (now - _lastMovementTime).TotalSeconds;

            // Initialise descent on first frame
            if (!_descentInitialized)
            {
                _descentInitialized = true;
                _isDescending = true;
                _descentStartY = theObject.ObjectOffsets?.y ?? DescentStartOffsetY;
                if (_descentStartY > DescentTargetOffsetY)
                    _descentStartY = DescentStartOffsetY;
                _descentStartTime = now;
                theObject.ObjectOffsets.y = _descentStartY;
            }

            if (_isDescending)
            {
                // Track ship world position during descent so the mothership stays in view
                // (same pattern as MotherShipSmall). After descent completes, tracking stops.
                SyncMovement(theObject);
                var shipWp = GetShipMapPosition();
                theObject.WorldPosition = new Vector3 { x = shipWp.x, y = 0, z = shipWp.z + DescentSpawnOffsetZ };

                float elapsed = (float)(now - _descentStartTime).TotalSeconds;
                float t = Math.Clamp(elapsed / DescentDurationSeconds, 0f, 1f);
                float smoothT = 1f - (1f - t) * (1f - t);
                float descentTarget = GameState.SurfaceState.GlobalMapPosition.y * SyncFactorY + SyncAnchorY;
                theObject.ObjectOffsets.y = _descentStartY + (descentTarget - _descentStartY) * smoothT;

                // Spin slowly during descent
                Zrotation += SpinSpeed * (float)deltaSeconds;
                if (Zrotation > 360f) Zrotation -= 360f;

                // Engines tilt based on ship motion
                AnimateEngines(theObject, (float)deltaSeconds, ComputeTargetTilt((float)deltaSeconds, theObject));

                // Release particles from both wing engines every frame (current-frame animated guides)
                var leftStart = GetCurrentFrameRotatedGuide(theObject, "LeftWingEngineStart", Xrotation, Yrotation, Zrotation);
                var leftGuide = GetCurrentFrameRotatedGuide(theObject, "LeftWingEngineGuide", Xrotation, Yrotation, Zrotation);
                var rightStart = GetCurrentFrameRotatedGuide(theObject, "RightWingEngineStart", Xrotation, Yrotation, Zrotation);
                var rightGuide = GetCurrentFrameRotatedGuide(theObject, "RightWingEngineGuide", Xrotation, Yrotation, Zrotation);
                ReleaseWingParticles(theObject, leftStart, leftGuide);
                ReleaseWingParticles(theObject, rightStart, rightGuide);

                if (t >= 1f)
                {
                    _isDescending = false;
                }
            }
            else
            {
                // At combat altitude: hover in place like MotherShipSmall (no travel, no live tracking).
                // Heading is re-sampled only between shots so aiming is not continuous.
                SyncMovement(theObject);
                UpdateFacingBetweenShots(theObject);

                float maxDelta = GetRotationDegreesPerSecond() * (float)deltaSeconds;
                Xrotation = OmegaObjectHelpers.MoveAngleTowards(Xrotation, TargetXrotation, maxDelta);
                Yrotation = OmegaObjectHelpers.MoveAngleTowards(Yrotation, TargetYrotation, maxDelta);
                Zrotation = OmegaObjectHelpers.MoveAngleTowards(Zrotation, TargetZrotation, maxDelta);

                // Engines tilt based on ship motion
                AnimateEngines(theObject, (float)deltaSeconds, ComputeTargetTilt((float)deltaSeconds, theObject));

                // Release particles from both wing engines (current-frame animated guides)
                var leftStart = GetCurrentFrameRotatedGuide(theObject, "LeftWingEngineStart", Xrotation, Yrotation, Zrotation);
                var leftGuide = GetCurrentFrameRotatedGuide(theObject, "LeftWingEngineGuide", Xrotation, Yrotation, Zrotation);
                var rightStart = GetCurrentFrameRotatedGuide(theObject, "RightWingEngineStart", Xrotation, Yrotation, Zrotation);
                var rightGuide = GetCurrentFrameRotatedGuide(theObject, "RightWingEngineGuide", Xrotation, Yrotation, Zrotation);
                ReleaseWingParticles(theObject, leftStart, leftGuide);
                ReleaseWingParticles(theObject, rightStart, rightGuide);

                UpdateFire(theObject, audioPlayer, soundRegistry);
            }

            theObject.Rotation.x = Xrotation;
            theObject.Rotation.y = Yrotation;
            theObject.Rotation.z = Zrotation;

            UpdateEngineSound(theObject);
            UpdateFlash(theObject);

            if (theObject.WeaponSystems != null)
                theObject.WeaponSystems.MoveWeapon(audioPlayer, soundRegistry);

            if (ParentObject.Particles?.Particles.Count > 0)
                ParentObject.Particles.MoveParticles();

            TerrainAvoidanceHelpers.ApplyTerrainRecovery(theObject, (float)deltaSeconds);
            SyncToOriginal(theObject);

            _lastMovementTime = now;
            return theObject;
        }

        // -------------------------------------------------------
        //  Crash / damage / explosion
        // -------------------------------------------------------
        private void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null) return;

            int maxHealth = EnemySetup.GetMotherShipHealth(theObject.ObjectName, GetAggression());
            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? maxHealth;
            bool isShipCollision = theObject.ImpactStatus.ObjectName == "Ship";
            if (isShipCollision && (DateTime.Now - _shipCollisionCooldown).TotalSeconds < 2.0)
            {
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

            int damage = theObject.ImpactStatus.ObjectName switch
            {
                "Ship" => (int)(maxHealth * ShipRamDamagePercent),
                string objectName when WeaponSetup.IsWeaponTypeValid(objectName) => WeaponSetup.GetWeaponDamage(objectName),
                _ => 0
            };

            if (damage <= 0)
            {
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

            if (isShipCollision)
                _shipCollisionCooldown = DateTime.Now;

            theObject.ImpactStatus.ObjectHealth = currentHealth - damage;

            if (theObject.ImpactStatus.ObjectHealth > 0)
            {
                HitSparkEffects.ReleaseHitSparks(theObject, this, theObject.ImpactStatus.ObjectName);
                PlayThudSound(theObject);
                StartFlash(theObject);
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

            StartExplosion(theObject);
            theObject.ImpactStatus.HasCrashed = false;
        }

        private void StartFlash(I3dObject theObject)
        {
            if (_originalColors == null)
            {
                _originalColors = new List<List<string?>>(theObject.ObjectParts.Count);
                foreach (var part in theObject.ObjectParts)
                {
                    var partColors = new List<string?>(part.Triangles.Count);
                    foreach (var tri in part.Triangles)
                        partColors.Add(tri.Color);
                    _originalColors.Add(partColors);
                }
            }

            foreach (var part in theObject.ObjectParts)
                foreach (var tri in part.Triangles)
                    tri.Color = FlashColor;

            _isFlashing = true;
            _flashStartTime = DateTime.Now;
        }

        private void UpdateFlash(I3dObject theObject)
        {
            if (!_isFlashing || _originalColors == null) return;

            if ((DateTime.Now - _flashStartTime).TotalSeconds >= FlashDurationSeconds)
            {
                for (int p = 0; p < theObject.ObjectParts.Count && p < _originalColors.Count; p++)
                {
                    var tris = theObject.ObjectParts[p].Triangles;
                    var colors = _originalColors[p];
                    for (int t = 0; t < tris.Count && t < colors.Count; t++)
                        tris[t].Color = colors[t];
                }
                _isFlashing = false;
            }
        }

        private void StartExplosion(I3dObject theObject)
        {
            if (_isExploding) return;

            _engineInstance?.Stop(playEndSegment: false);
            _engineInstance = null;

            PlayExplosionSound(theObject);

            _isExploding = true;
            _secondExplosionTriggered = false;
            _explosionDeltaTime = DateTime.Now;
            _explosionWorldPosition = theObject.WorldPosition as Vector3 ?? new Vector3 { x = theObject.WorldPosition.x, y = theObject.WorldPosition.y, z = theObject.WorldPosition.z };
            _explosionObjectOffsets = theObject.ObjectOffsets as Vector3 ?? new Vector3 { x = theObject.ObjectOffsets.x, y = theObject.ObjectOffsets.y, z = theObject.ObjectOffsets.z };

            ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
            Physics.ExplodeObject(theObject, FirstExplosionForce);
            theObject.CrashBoxes = new List<List<IVector3>>();
        }

        private void PlayThudSound(I3dObject theObject)
        {
            if (_audio == null || _thudSound == null) return;
            var audioPosition = ((_3dObject)theObject).GetAudioPosition();
            _audio.Play(_thudSound, AudioPlayMode.OneShot, new AudioPlayOptions
            {
                WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
            });
        }

        private void PlayExplosionSound(I3dObject theObject)
        {
            if (_audio == null || _explosionSound == null) return;
            var audioPosition = ((_3dObject)theObject).GetAudioPosition();
            _audio.Play(_explosionSound, AudioPlayMode.OneShot, new AudioPlayOptions
            {
                WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
            });
        }

        // -------------------------------------------------------
        //  Engine sound
        // -------------------------------------------------------
        private void UpdateEngineSound(I3dObject theObject)
        {
            if (_audio == null || _engineSound == null) return;

            if (theObject.IsOnScreen)
            {
                var audioPosition = ((_3dObject)theObject).GetAudioPosition();

                if (_engineInstance == null || !_engineInstance.IsPlaying)
                {
                    _engineInstance = _audio.Play(
                        _engineSound,
                        AudioPlayMode.SegmentedLoop,
                        new AudioPlayOptions
                        {
                            WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                        });
                }

                _engineInstance.SetVolume(_engineSound.Settings.Volume);
                _engineInstance.SetWorldPosition(new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z));
            }
            else
            {
                var globalPos = GameState.SurfaceState?.GlobalMapPosition;
                var mothershipWorldPos = theObject.WorldPosition;

                if (globalPos != null && mothershipWorldPos != null)
                {
                    float distSq = OmegaObjectHelpers.GetDistanceSquared(globalPos, mothershipWorldPos);
                    float maxDist = AudioSetup.OffscreenAiAudioMaxDistance;
                    float maxDistSq = maxDist * maxDist;

                    if (distSq <= maxDistSq)
                    {
                        float distance = MathF.Sqrt(distSq);
                        float normalized = distance / maxDist;
                        float volume = _engineSound.Settings.Volume *
                            MathF.Pow(1f - normalized, AudioSetup.OffscreenAiAudioCurveExponent);

                        if (_engineInstance == null || !_engineInstance.IsPlaying)
                        {
                            _engineInstance = _audio.Play(
                                _engineSound,
                                AudioPlayMode.SegmentedLoop,
                                new AudioPlayOptions
                                {
                                    WorldPosition = System.Numerics.Vector3.Zero
                                });
                        }

                        float dx = mothershipWorldPos.x - globalPos.x;
                        _engineInstance.SetWorldPosition(new System.Numerics.Vector3(dx, 0, 0));
                        _engineInstance.SetVolume(volume);
                    }
                    else if (_engineInstance != null)
                    {
                        _engineInstance.Stop(playEndSegment: false);
                        _engineInstance = null;
                    }
                }
                else if (_engineInstance != null)
                {
                    _engineInstance.Stop(playEndSegment: false);
                    _engineInstance = null;
                }
            }
        }

        // -------------------------------------------------------
        //  Surface sync
        // -------------------------------------------------------
        private void SyncMovement(I3dObject theObject)
        {
            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = SyncAnchorY;
            }

            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(theObject, _syncY, SyncFactorY);
        }

        // -------------------------------------------------------
        //  Facing
        // -------------------------------------------------------
        private bool _headingLocked = false;

        // Re-aim only between shots: once a shot is in flight the heading is frozen,
        // and a fresh heading snapshot is taken for the next shot. Matches MotherShipSmall
        // which locks its charging target position and releases after the ram.
        private void UpdateFacingBetweenShots(I3dObject theObject)
        {
            float fireIntervalSeconds = GetFireIntervalSeconds();
            float chargeWindowSeconds = GetChargeWindowSeconds();
            // Within the charge window (the last ChargeWindowSeconds before firing) keep
            // the previously locked heading — this is the "waiting until the shot is finished"
            // behaviour the user asked for.
            bool inChargeWindow = (fireIntervalSeconds - _fireTimer) <= chargeWindowSeconds;
            if (_headingLocked && inChargeWindow) return;

            var wp = theObject.WorldPosition;
            if (wp == null) return;

            var shipPos = GetShipMapPosition();
            float dx = shipPos.x - wp.x;
            float dz = shipPos.z - wp.z;
            if (MathF.Abs(dx) < 1f && MathF.Abs(dz) < 1f) return;

            var heading = OmegaObjectHelpers.GetHeadingFromDirection(dx, dz);
            TargetXrotation = heading.X;
            TargetYrotation = heading.Y;
            TargetZrotation = heading.Z;
            _headingLocked = true;
        }

        private void UpdateFacingTowardsShip(I3dObject theObject, float deltaSeconds)
        {
            var now = DateTime.Now;
            if ((now - _lastDirectionUpdateTime).TotalSeconds < GetDirectionUpdateIntervalSeconds())
                return;
            _lastDirectionUpdateTime = now;

            var wp = theObject.WorldPosition;
            if (wp == null) return;

            var shipPos = GetShipMapPosition();
            float dx = shipPos.x - wp.x;
            float dz = shipPos.z - wp.z;

            if (MathF.Abs(dx) < 1f && MathF.Abs(dz) < 1f) return;

            var heading = OmegaObjectHelpers.GetHeadingFromDirection(dx, dz);
            TargetXrotation = heading.X;
            TargetYrotation = heading.Y;
            TargetZrotation = heading.Z;
        }

        private static Vector3 GetShipMapPosition()
        {
            var map = GameState.SurfaceState.GlobalMapPosition;
            return new Vector3 { x = map.x, y = 0, z = map.z };
        }

        private ITriangleMeshWithColor? GetCurrentFrameRotatedGuide(I3dObject theObject, string partName, float rotX, float rotY, float rotZ)
        {
            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part?.Triangles == null || part.Triangles.Count == 0)
                return null;

            var src = part.Triangles[0];
            var tri = new TriangleMeshWithColor
            {
                Color = src.Color,
                noHidden = src.noHidden,
                angle = src.angle,
                vert1 = new Vector3 { x = src.vert1.x, y = src.vert1.y, z = src.vert1.z },
                vert2 = new Vector3 { x = src.vert2.x, y = src.vert2.y, z = src.vert2.z },
                vert3 = new Vector3 { x = src.vert3.x, y = src.vert3.y, z = src.vert3.z },
            };

            var mesh = new List<ITriangleMeshWithColor> { tri };
            mesh = _rotate.RotateZMesh(mesh, rotZ);
            mesh = _rotate.RotateYMesh(mesh, rotY);
            mesh = _rotate.RotateXMesh(mesh, rotX);

            return mesh[0];
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle < 0f) angle += 360f;
            return angle;
        }

        // -------------------------------------------------------
        //  Particles
        // -------------------------------------------------------
        private void ReleaseWingParticles(I3dObject theObject, ITriangleMeshWithColor? start, ITriangleMeshWithColor? guide)
        {
            if (start == null || guide == null || ParentObject?.Particles == null) return;
            var worldPos = (Vector3)theObject.WorldPosition;

            var startPos = new Vector3(
                (start.vert1.x + start.vert2.x + start.vert3.x) / 3f,
                (start.vert1.y + start.vert2.y + start.vert3.y) / 3f,
                (start.vert1.z + start.vert2.z + start.vert3.z) / 3f);
            var guidePos = new Vector3(
                (guide.vert1.x + guide.vert2.x + guide.vert3.x) / 3f,
                (guide.vert1.y + guide.vert2.y + guide.vert3.y) / 3f,
                (guide.vert1.z + guide.vert2.z + guide.vert3.z) / 3f);

            // Reverse stream direction while keeping spawn anchored at engine start.
            var reversedGuidePos = new Vector3
            {
                x = 2f * startPos.x - guidePos.x,
                y = 2f * startPos.y - guidePos.y,
                z = 2f * startPos.z - guidePos.z
            };
            ITriangleMeshWithColor reversedGuide = new TriangleMeshWithColor
            {
                Color = guide.Color,
                noHidden = guide.noHidden,
                angle = guide.angle,
                vert1 = new Vector3 { x = reversedGuidePos.x, y = reversedGuidePos.y, z = reversedGuidePos.z },
                vert2 = new Vector3 { x = reversedGuidePos.x, y = reversedGuidePos.y, z = reversedGuidePos.z },
                vert3 = new Vector3 { x = reversedGuidePos.x, y = reversedGuidePos.y, z = reversedGuidePos.z },
            };

            int count = _isDescending ? 2 : 4;

            ParentObject.Particles.ReleaseParticles(
                reversedGuide,
                start,
                worldPos,
                this,
                count,
                false);
        }

        // -------------------------------------------------------
        //  Engine tilt — continuous 360° rotation so motion is always visible
        // -------------------------------------------------------
        // -------------------------------------------------------
        //  Engine tilt — thrust-vectoring gimbal driven by ship motion.
        //  At rest the pods sit at 0° (nozzles aft, as built). When the ship moves
        //  the pods tilt so the nozzle points along the motion vector — thrust
        //  exhausts opposite the motion (nose down while descending, nose up while
        //  climbing, etc.).
        // -------------------------------------------------------
        private float _tiltTestTime = 0f;
        private float _prevOffsetsY = float.NaN;
        private Vector3? _prevWorldPos;
        private float _smoothedTilt = 0f;

        // Tuning: how many world-units/sec of vertical motion saturates the gimbal
        private const float TiltVerticalSaturationUps = 400f;
        private const float TiltSmoothingPerSecond = 4f; // exponential smoothing rate

        private float ComputeTargetTilt(float deltaSeconds, I3dObject theObject)
        {
            _tiltTestTime += deltaSeconds;

            // During descent: keep pods locked at 0° (nozzles aft) — the ship is falling
            // into its resting position and should not be twisting the engines yet.
            if (_isDescending)
            {
                _prevOffsetsY = theObject.ObjectOffsets?.y ?? 0f;
                _smoothedTilt = 0f;
                return 0f;
            }

            // Post-descent (hover/combat): lock the pods at the forward-flight gimbal
            // position. Nozzles point so their thrust vector drives the ship forward;
            // they do NOT oscillate here.
            float targetAngle = MaxGimbalDegrees;

            // Exponential smoothing so the handoff from descent (0°) to forward-flight
            // lock is a smooth ease-in rather than a snap.
            float alpha = 1f - MathF.Exp(-TiltSmoothingPerSecond * deltaSeconds);
            _smoothedTilt += (targetAngle - _smoothedTilt) * alpha;
            return _smoothedTilt;
        }

        // Pivot points for the left and right pod assemblies — computed once from the
        // LeftPod / RightPod bounding box centroid. All parts of the assembly rotate
        // around this pivot so the pod twists around its own wing mount point (thrust
        // vectoring), not pendulums through the ship centerline.
        private Vector3? _leftPodPivot;
        private Vector3? _rightPodPivot;
        private void AnimateEngines(I3dObject theObject, float deltaSeconds, float targetTilt)
        {
            // Capture original (unrotated) triangle baselines on the very first call
            if (_leftEngineOriginalTris == null)
            {
                _leftEngineOriginalTris  = CopyTriangles(theObject, "LeftWingEngine");
                _rightEngineOriginalTris = CopyTriangles(theObject, "RightWingEngine");
                _leftEngineGuideOriginalTris  = CopyTriangles(theObject, "LeftWingEngineGuide");
                _rightEngineGuideOriginalTris = CopyTriangles(theObject, "RightWingEngineGuide");
                _leftEngineStartOriginalTris  = CopyTriangles(theObject, "LeftWingEngineStart");
                _rightEngineStartOriginalTris = CopyTriangles(theObject, "RightWingEngineStart");
                _leftPodOriginalTris          = CopyTriangles(theObject, "LeftPod");
                _rightPodOriginalTris         = CopyTriangles(theObject, "RightPod");
                _leftConnectorOriginalTris    = CopyTriangles(theObject, "LeftConnector");
                _rightConnectorOriginalTris   = CopyTriangles(theObject, "RightConnector");
                _podNacelleVentsOriginalTris  = CopyTriangles(theObject, "PodNacelleVents");

                // Pivot = centroid of the pod housing's own AABB — this is the wing-mount point.
                if (_leftPodOriginalTris!.Count > 0)  _leftPodPivot  = GetPartCenter(_leftPodOriginalTris);
                if (_rightPodOriginalTris!.Count > 0) _rightPodPivot = GetPartCenter(_rightPodOriginalTris);
            }

            // Drive tilt directly from the oscillator so movement is always visible
            _engineTiltAngle = targetTilt;

            // Twist the LEFT pod assembly around its own wing-mount pivot
            if (_leftPodPivot != null)
            {
                var lp = _leftPodPivot;
                ApplyPivotedRotation(theObject, "LeftPod",             _leftPodOriginalTris,         lp, -_engineTiltAngle);
                ApplyPivotedRotation(theObject, "LeftConnector",       _leftConnectorOriginalTris,   lp, -_engineTiltAngle);
                ApplyPivotedRotation(theObject, "LeftWingEngine",      _leftEngineOriginalTris,      lp, -_engineTiltAngle);
                ApplyPivotedRotation(theObject, "LeftWingEngineGuide", _leftEngineGuideOriginalTris, lp, -_engineTiltAngle);
                ApplyPivotedRotation(theObject, "LeftWingEngineStart", _leftEngineStartOriginalTris, lp, -_engineTiltAngle);
            }

            // Twist the RIGHT pod assembly around its own wing-mount pivot (opposite sign
            // so both pods tilt their nozzles the same direction in world space)
            if (_rightPodPivot != null)
            {
                var rp = _rightPodPivot;
                ApplyPivotedRotation(theObject, "RightPod",             _rightPodOriginalTris,         rp, -_engineTiltAngle);
                ApplyPivotedRotation(theObject, "RightConnector",       _rightConnectorOriginalTris,   rp, -_engineTiltAngle);
                ApplyPivotedRotation(theObject, "RightWingEngine",      _rightEngineOriginalTris,      rp, -_engineTiltAngle);
                ApplyPivotedRotation(theObject, "RightWingEngineGuide", _rightEngineGuideOriginalTris, rp, -_engineTiltAngle);
                ApplyPivotedRotation(theObject, "RightWingEngineStart", _rightEngineStartOriginalTris, rp, -_engineTiltAngle);
            }

            // Nacelle vents span both pods — leave static (otherwise they'd tear apart).
            _ = _podNacelleVentsOriginalTris; // suppress unused-warning for cached copy

            // Animate engine glow color based on movement fraction
            float moveFraction = MathF.Min(_prevMovementSpeed * 0.05f, 1f);
            string engineColor = InterpolateEngineColor(moveFraction);
            SetEnginePartColor(theObject, "LeftWingEngine",  engineColor);
            SetEnginePartColor(theObject, "RightWingEngine", engineColor);

            // Spin the cannon muzzle ball around the barrel axis (X)
            AnimateCannonMuzzle(theObject, deltaSeconds);
        }

        // Rotate a part around a local pivot on the Z axis (pitch gimbal in the ship's
        // local frame: nose-up/nose-down). Z-axis rotation keeps the lateral (Y) position
        // fixed, so the pod stays on its wing while the fore/aft end swings up and down.
        private void ApplyPivotedRotation(I3dObject theObject, string partName,
            List<ITriangleMeshWithColor>? originalTris, Vector3 pivot, float angle)
        {
            if (originalTris == null || originalTris.Count == 0) return;
            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part == null) return;

            var atOrigin = TranslateMesh(originalTris, new Vector3 { x = -pivot.x, y = -pivot.y, z = -pivot.z });
            var rotated  = _rotate.RotateYMesh(atOrigin, angle);
            part.Triangles = TranslateMesh(rotated, pivot);
        }

        // -------------------------------------------------------
        //  Cannon muzzle ball spin — rotate the FrontCannonMuzzle cone around its
        //  own barrel axis (X). Pivot is the muzzle tip on the barrel centerline.
        // -------------------------------------------------------
        private void AnimateCannonMuzzle(I3dObject theObject, float deltaSeconds)
        {
            if (_muzzleOriginalTris == null)
            {
                _muzzleOriginalTris = CopyTriangles(theObject, "FrontCannonMuzzle");
            }
            if (_muzzleOriginalTris == null || _muzzleOriginalTris.Count == 0) return;

            _muzzleSpinAngle += MuzzleSpinSpeed * deltaSeconds;
            if (_muzzleSpinAngle >= 360f) _muzzleSpinAngle -= 360f;

            var part = theObject.ObjectParts.Find(p => p.PartName == "FrontCannonMuzzle");
            if (part == null) return;

            // Pivot on the barrel centerline (y=0, z=0); X doesn't matter for an X-axis rotation.
            var pivot = new Vector3 { x = 0f, y = 0f, z = 0f };
            var atOrigin = TranslateMesh(_muzzleOriginalTris, new Vector3 { x = -pivot.x, y = -pivot.y, z = -pivot.z });
            var rotated  = _rotate.RotateXMesh(atOrigin, _muzzleSpinAngle);
            part.Triangles = TranslateMesh(rotated, pivot);
        }

        private static List<ITriangleMeshWithColor> CopyTriangles(I3dObject theObject, string partName)
        {
            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part?.Triangles == null) return new List<ITriangleMeshWithColor>();
            var copy = new List<ITriangleMeshWithColor>(part.Triangles.Count);
            foreach (var tri in part.Triangles)
                copy.Add(new TriangleMeshWithColor
                {
                    Color = tri.Color, noHidden = tri.noHidden, angle = tri.angle,
                    vert1 = new Vector3 { x = tri.vert1.x, y = tri.vert1.y, z = tri.vert1.z },
                    vert2 = new Vector3 { x = tri.vert2.x, y = tri.vert2.y, z = tri.vert2.z },
                    vert3 = new Vector3 { x = tri.vert3.x, y = tri.vert3.y, z = tri.vert3.z },
                });
            return copy;
        }

        private void ApplyEngineRotation(I3dObject theObject, string partName, List<ITriangleMeshWithColor>? originalTris)
        {
            if (originalTris == null || originalTris.Count == 0) return;
            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part == null) return;

            // Rotate around X axis with the pivot on the SHIP CENTERLINE (y=0, z=0).
            // The nozzles sit at y=±90 from centerline → a ±35° X-rotation swings them
            // vertically by ~90·sin(angle) units, a very visible sweep from the camera angle.
            // X-axis rotation leaves X unchanged, so the nozzle's forward/back position is preserved.
            var rotated = _rotate.RotateXMesh(originalTris, _engineTiltAngle);
            part.Triangles = rotated;
        }

        private static string InterpolateEngineColor(float t)
        {
            t = MathF.Max(0f, MathF.Min(1f, t));
            float flicker = (_flickerRng.NextSingle() - 0.5f) * 2f * EngineFlickerAmount;
            t = MathF.Max(0f, MathF.Min(1f, t + flicker));
            int r = (int)(EngineIdleRgb.r + (EngineActiveRgb.r - EngineIdleRgb.r) * t);
            int g = (int)(EngineIdleRgb.g + (EngineActiveRgb.g - EngineIdleRgb.g) * t);
            int b = (int)(EngineIdleRgb.b + (EngineActiveRgb.b - EngineIdleRgb.b) * t);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        private static void SetEnginePartColor(I3dObject theObject, string partName, string color)
        {
            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part == null) return;
            for (int i = 0; i < part.Triangles.Count; i++)
            {
                var tri = part.Triangles[i];
                tri.Color = color;
                part.Triangles[i] = tri;
            }
        }

        private static Vector3 GetPartCenter(List<ITriangleMeshWithColor> triangles)
        {
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            foreach (var t in triangles)
            {
                foreach (var v in new[] { (Vector3)t.vert1, (Vector3)t.vert2, (Vector3)t.vert3 })
                {
                    if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                    if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
                    if (v.z < minZ) minZ = v.z; if (v.z > maxZ) maxZ = v.z;
                }
            }
            return new Vector3 { x = (minX + maxX) * 0.5f, y = (minY + maxY) * 0.5f, z = (minZ + maxZ) * 0.5f };
        }

        private static List<ITriangleMeshWithColor> TranslateMesh(List<ITriangleMeshWithColor> triangles, Vector3 offset)
        {
            var result = new List<ITriangleMeshWithColor>(triangles.Count);
            foreach (var tri in triangles)
            {
                result.Add(new TriangleMeshWithColor
                {
                    Color = tri.Color,
                    noHidden = tri.noHidden,
                    angle = tri.angle,
                    vert1 = new Vector3 { x = tri.vert1.x + offset.x, y = tri.vert1.y + offset.y, z = tri.vert1.z + offset.z },
                    vert2 = new Vector3 { x = tri.vert2.x + offset.x, y = tri.vert2.y + offset.y, z = tri.vert2.z + offset.z },
                    vert3 = new Vector3 { x = tri.vert3.x + offset.x, y = tri.vert3.y + offset.y, z = tri.vert3.z + offset.z },
                });
            }
            return result;
        }

        // -------------------------------------------------------
        //  Ship-follow with dead-zone
        // -------------------------------------------------------
        private const float TrackingDeadZone = 600f;   // ship must move this far before mothership retargets
        private float _lastTrackedShipX = float.NaN;
        private float _lastTrackedShipZ = float.NaN;

        private void UpdateWorldTravel(I3dObject theObject, float deltaSeconds)
        {
            var map = GameState.SurfaceState.GlobalMapPosition;

            // Only retarget when ship has moved outside the dead-zone from last tracked position
            bool firstTime = float.IsNaN(_travelTargetX) || float.IsNaN(_lastTrackedShipX);
            float shipMovedDist = firstTime ? float.MaxValue : MathF.Sqrt(
                (map.x - _lastTrackedShipX) * (map.x - _lastTrackedShipX) +
                (map.z - _lastTrackedShipZ) * (map.z - _lastTrackedShipZ));

            if (firstTime || shipMovedDist >= TrackingDeadZone)
            {
                _lastTrackedShipX = map.x;
                _lastTrackedShipZ = map.z;

                var wp0 = theObject.WorldPosition;
                float tdx = map.x - wp0.x;
                float tdz = map.z - wp0.z;
                float tdist = MathF.Sqrt(tdx * tdx + tdz * tdz);
                if (tdist > StandoffDistance)
                {
                    float inv = 1f / tdist;
                    _travelTargetX = map.x - tdx * inv * StandoffDistance;
                    _travelTargetZ = map.z - tdz * inv * StandoffDistance;
                }
                else
                {
                    _travelTargetX = wp0.x;
                    _travelTargetZ = wp0.z;
                }
            }

            // Move WorldPosition toward the target at capped speed
            var wp = theObject.WorldPosition;
            float dx = _travelTargetX - wp.x;
            float dz = _travelTargetZ - wp.z;
            float dist = MathF.Sqrt(dx * dx + dz * dz);
            float step = MotherShipDifficultySetup.ScaleTravelSpeed(WorldTravelSpeed, GetAggression()) * deltaSeconds;

            if (dist > step)
            {
                float inv = 1f / dist;
                theObject.WorldPosition = new Vector3
                {
                    x = wp.x + dx * inv * step,
                    y = 0,
                    z = wp.z + dz * inv * step
                };
            }
            else if (dist > 0.1f)
            {
                theObject.WorldPosition = new Vector3 { x = _travelTargetX, y = 0, z = _travelTargetZ };
            }
        }

        // -------------------------------------------------------
        //  Sync back to AiObjects
        // -------------------------------------------------------
        private static void SyncToOriginal(I3dObject deepCopy)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null) return;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectId == deepCopy.ObjectId)
                {
                    var original = aiObjects[i];
                    if (ReferenceEquals(original, deepCopy)) return;
                    original.WorldPosition = new Vector3
                    {
                        x = deepCopy.WorldPosition.x,
                        y = deepCopy.WorldPosition.y,
                        z = deepCopy.WorldPosition.z
                    };
                    original.ObjectOffsets = new Vector3
                    {
                        x = deepCopy.ObjectOffsets.x,
                        y = deepCopy.ObjectOffsets.y,
                        z = deepCopy.ObjectOffsets.z
                    };
                    return;
                }
            }
        }

        // -------------------------------------------------------
        //  Audio
        // -------------------------------------------------------
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured) return;
            if (audioPlayer == null || soundRegistry == null) return;

            _audio = audioPlayer;
            _thudSound = soundRegistry.Get("lazer_thud");
            _explosionSound = soundRegistry.Get("explosion_main");
            if (soundRegistry.TryGet("lazer_charge", out var chargeSound))
                _chargeSound = chargeSound;
            if (soundRegistry.TryGet("mothership_lazer", out var lazerFireSound))
                _lazerFireSound = lazerFireSound;
            if (soundRegistry.TryGet("mothership_engine", out var engineSound))
                _engineSound = engineSound;
            if (soundRegistry.TryGet("mothership_charge_imminent", out var imminentSound))
                _imminentSound = imminentSound;
            _audioConfigured = true;
        }

        // -------------------------------------------------------
        //  Guide coordinate setters
        // -------------------------------------------------------
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) _leftEngineStart = StartCoord;
            if (GuideCoord != null) _leftEngineGuide = GuideCoord;
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) _rightEngineStart = StartCoord;
            if (GuideCoord != null) _rightEngineGuide = GuideCoord;
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) _weaponStartGuide = StartCoord;
            if (GuideCoord != null) _weaponDirectionGuide = GuideCoord;
            if (Logger.ShouldLog(enableLogging))
            {
                if (StartCoord != null)
                    Logger.Log($"[MotherShipMedium] WeaponStartGuide set: x={StartCoord.vert1.x:F1}; y={StartCoord.vert1.y:F1}; z={StartCoord.vert1.z:F1}", "MSM");
                if (GuideCoord != null)
                    Logger.Log($"[MotherShipMedium] WeaponDirectionGuide set: x={GuideCoord.vert1.x:F1}; y={GuideCoord.vert1.y:F1}; z={GuideCoord.vert1.z:F1}", "MSM");
            }
        }

        // -------------------------------------------------------
        //  Fire
        // -------------------------------------------------------
        private void UpdateFire(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            float fireIntervalSeconds = GetFireIntervalSeconds();
            _fireTimer += GameState.DeltaTime;

            UpdateChargeRings(theObject);

            if (_fireTimer < fireIntervalSeconds)
                return;

            // Only fire when guide geometry is available
            if (_weaponStartGuide == null || _weaponDirectionGuide == null)
            {
                if (Logger.ShouldLog(enableLogging))
                    Logger.Log($"[MotherShipMedium] FIRE SKIPPED — guides not set (start={_weaponStartGuide != null}; dir={_weaponDirectionGuide != null})", "MSM");
                return;
            }

            _fireTimer = 0f;
            _chargeSoundPlayed = false;
            _headingLocked = false; // release heading so next cycle samples a fresh ship position

            if (_chargeInstance != null)
            {
                _chargeInstance.Stop(playEndSegment: false);
                _chargeInstance = null;
            }

            if (Logger.ShouldLog(enableLogging))
            {
                Logger.Log(
                    $"[MotherShipMedium] FireWeapon — start=(x={_weaponStartGuide.vert1.x:F1}; y={_weaponStartGuide.vert1.y:F1}; z={_weaponStartGuide.vert1.z:F1}) " +
                    $"dir=(x={_weaponDirectionGuide.vert1.x:F1}; y={_weaponDirectionGuide.vert1.y:F1}; z={_weaponDirectionGuide.vert1.z:F1}) " +
                    $"worldMap=(x={GameState.SurfaceState.GlobalMapPosition.x:F1}; y={GameState.SurfaceState.GlobalMapPosition.y:F1}; z={GameState.SurfaceState.GlobalMapPosition.z:F1}) " +
                    $"objOffsets=(x={theObject.ObjectOffsets.x:F1}; y={theObject.ObjectOffsets.y:F1}; z={theObject.ObjectOffsets.z:F1})",
                    "MSM");
            }

            theObject.WeaponSystems?.FireWeapon(
                _weaponDirectionGuide.vert1,
                _weaponStartGuide.vert1,
                theObject.WorldPosition,
                WeaponType.Lazer,
                theObject,
                0);

            if (_audio != null && _lazerFireSound != null)
            {
                var audioPos = ((_3dObject)theObject).GetAudioPosition();
                _audio.PlayOneShot(
                    _lazerFireSound,
                    new AudioPlayOptions
                    {
                        WorldPosition = new System.Numerics.Vector3(audioPos.x, audioPos.y, audioPos.z)
                    });
            }
        }

        private void UpdateChargeRings(I3dObject theObject)
        {
            var ring1 = theObject.ObjectParts.Find(p => p.PartName == "CannonChargeRing1");
            var ring2 = theObject.ObjectParts.Find(p => p.PartName == "CannonChargeRing2");
            var ring3 = theObject.ObjectParts.Find(p => p.PartName == "CannonChargeRing3");

            float chargeWindowSeconds = GetChargeWindowSeconds();
            float timeUntilFire = GetFireIntervalSeconds() - _fireTimer;
            float step = chargeWindowSeconds / 3f;

            bool r1 = timeUntilFire <= chargeWindowSeconds;
            bool r2 = timeUntilFire <= chargeWindowSeconds - step;
            bool r3 = timeUntilFire <= chargeWindowSeconds - step * 2f;

            if (ring1 != null) ring1.IsVisible = r1;
            if (ring2 != null) ring2.IsVisible = r2;
            if (ring3 != null) ring3.IsVisible = r3;

            if (r1 && !_chargeSoundPlayed && _audio != null && _chargeSound != null)
            {
                _chargeSoundPlayed = true;
                var audioPos = ((_3dObject)theObject).GetAudioPosition();
                _chargeInstance = _audio.Play(
                    _chargeSound,
                    AudioPlayMode.OneShot,
                    new AudioPlayOptions
                    {
                        WorldPosition = new System.Numerics.Vector3(audioPos.x, audioPos.y, audioPos.z)
                    });
                if (_imminentSound != null)
                    _audio.Play(
                        _imminentSound,
                        AudioPlayMode.OneShot,
                        new AudioPlayOptions
                        {
                            WorldPosition = System.Numerics.Vector3.Zero
                        });
            }

        }

        private static float GetFireIntervalSeconds()
        {
            return MotherShipDifficultySetup.ScaleCooldown(FireIntervalSeconds, GetAggression());
        }

        private static float GetRotationDegreesPerSecond()
        {
            return MotherShipDifficultySetup.ScaleTurnSpeed(RotationDegreesPerSecond, GetAggression());
        }

        private static float GetDirectionUpdateIntervalSeconds()
        {
            return MotherShipDifficultySetup.ScaleUpdateInterval(DirectionUpdateIntervalSeconds, GetAggression());
        }

        private static float GetChargeWindowSeconds()
        {
            return MotherShipDifficultySetup.ScaleChargeWindow(ChargeWindowSeconds, GetAggression());
        }

        private static float GetAggression()
        {
            return MotherShipDifficultySetup.GetAggression(GameState.GamePlayState.MotherShipMediumAggression);
        }

        public void Dispose()
        {
            _syncInitialized = false;
            _syncY = SyncAnchorY;
            _descentInitialized = false;
            _isDescending = true;
            _leftEngineStart = null;
            _leftEngineGuide = null;
            _rightEngineStart = null;
            _rightEngineGuide = null;
            _engineTiltAngle = 0f;
            _leftEngineOriginalTris = null;
            _rightEngineOriginalTris = null;
            _leftEngineGuideOriginalTris = null;
            _rightEngineGuideOriginalTris = null;
            _muzzleOriginalTris = null;
            _muzzleSpinAngle = 0f;
            _prevMovementSpeed = 0f;
            _engineInstance?.Stop(playEndSegment: false);
            _engineInstance = null;
            _lastTrackedShipX = float.NaN;
            _lastTrackedShipZ = float.NaN;
            _isExploding = false;
            _isFlashing = false;
            _originalColors = null;
            _travelTargetX = float.NaN;
            _travelTargetZ = float.NaN;
            _headingLocked = false;
        }
    }
}
