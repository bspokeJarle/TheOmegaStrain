using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Audio.Services;
using TheOmegaStrain.Gameplay.Helpers;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Gameplay.Controls.MotherShipSmallControls
{
    public class MotherShipSmallControls : IObjectMovement
    {
        // Visual rotation:
        private const float BaseXRotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private const float BaseYRotation = 0f;
        private const float BaseZRotation = 0f;

        // Ship-facing rotation:
        private const float RotationDegreesPerSecond = 45f;
        private const float DirectionUpdateIntervalSeconds = 0.5f;

        // Sync offsets:
        private const float SyncFactorY = 2.5f;
        private const float SyncAnchorY = -25f;

        // Weak spot animation:
        private const float WeakSpotSpinSpeed = 1.5f;
        private const float PulsateAmplitude = 0.06f;
        private const float PulsateSpeed = 0.04f;
        private const float WeakSpotCenterX = -13.2f;
        private const float WeakSpotCenterY = 0f;
        private const float WeakSpotCenterZ = 69.6f;

        // Descent animation:
        private const float DescentDurationSeconds = 4.0f;
        private const float DescentSpawnOffsetX = 0f;
        private const float DescentSpawnOffsetZ = -1500f;

        // Explosion:
        private const float FirstExplosionForce = 200f;
        private const float SecondExplosionForce = 400f;
        private const float SecondExplosionDelaySeconds = 1.0f;

        // Hit flash:
        private const string FlashColor = "FFFFFF";
        private const string WeakSpotFlashColor = "FF2200";
        private const float FlashDurationSeconds = 0.15f;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        private float Yrotation = BaseYRotation;
        private float Xrotation = BaseXRotation;
        private float Zrotation = BaseZRotation;

        private float TargetXrotation = BaseXRotation;
        private float TargetYrotation = BaseYRotation;
        private float TargetZrotation = BaseZRotation;

        private DateTime _lastMovementTime = DateTime.MinValue;
        private DateTime _lastDirectionUpdateTime = DateTime.MinValue;

        private bool _syncInitialized = false;
        private float _syncY = 0;

        private float _weakSpotAngle = 0f;
        private float _pulsatePhase = 0f;
        private List<ITriangleMeshWithColor>? _weakSpotOriginalTris;

        private bool _audioConfigured = false;
        private bool _isExploding = false;
        private bool _secondExplosionTriggered = false;
        private DateTime _explosionDeltaTime = DateTime.Now;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;
        private IAudioPlayer? _audio;
        private SoundDefinition? _thudSound;
        private SoundDefinition? _explosionSound;
        private SoundDefinition? _engineSound;
        private IAudioInstance? _engineInstance;
        private SoundDefinition? _attackSound;
        private ISoundRegistry? _soundRegistry;
        private readonly ShipAiVoiceService _shipAiVoiceService = ShipAiVoiceService.Shared;
        private bool _attackSoundPlayed = false;
        private bool _warningSoundPlayed = false;
        private DateTime _shipCollisionCooldown = DateTime.MinValue;
        private DateTime _flashStartTime = DateTime.MinValue;
        private bool _isFlashing = false;
        private List<List<string?>>? _originalColors;

        private bool _isDescending = false;
        private float _descentStartY = 0f;
        private DateTime _descentStartTime = DateTime.MinValue;

        // Ram cycle state:
        private readonly MotherShipSmallAi.RamState _ramState = new();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ConfigureAudio(audioPlayer, soundRegistry);
            ParentObject = theObject;

            if (theObject.ImpactStatus?.HasExploded == true)
                return theObject;

            bool terrainRecoveryStarted = TerrainAvoidanceHelpers.TryStartTerrainRecovery(theObject);
            if (terrainRecoveryStarted)
                MotherShipSmallAi.ResetState(_ramState);

            if (theObject.ImpactStatus?.HasCrashed == true && !_isExploding)
            {
                HandleCrash(theObject);
            }

            if (_isExploding)
            {
                if (_explosionWorldPosition != null)
                    theObject.WorldPosition = _explosionWorldPosition;
                if (_explosionObjectOffsets != null)
                    theObject.ObjectOffsets = _explosionObjectOffsets;

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

                SyncMovement(theObject);
                SyncToOriginal(theObject);
                _lastMovementTime = DateTime.Now;
                return theObject;
            }

            var now = DateTime.Now;
            if (_lastMovementTime == DateTime.MinValue)
                _lastMovementTime = now;

            double deltaSeconds = (now - _lastMovementTime).TotalSeconds;

            // Engine sound: loop while alive, position-track each frame
            if (_audio != null && _engineSound != null && !_isExploding)
            {
                if (theObject.IsOnScreen)
                {
                    var audioPosition = ((OmegaObject3D)theObject).GetAudioPosition();

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

            // Descent animation: lower from starting altitude to surface level over DescentDurationSeconds
            if (!_isDescending && !_syncInitialized)
            {
                _isDescending = true;
                _descentStartY = theObject.ObjectOffsets.y;
                _descentStartTime = now;
            }

            if (_isDescending)
            {
                // Track the ship's position during descent so the mothership stays in view
                var shipWp = GetShipWorldPosition();
                theObject.WorldPosition = new Vector3
                {
                    x = shipWp.x + DescentSpawnOffsetX,
                    y = 0,
                    z = shipWp.z + DescentSpawnOffsetZ
                };

                float elapsed = (float)(now - _descentStartTime).TotalSeconds;
                float t = Math.Clamp(elapsed / DescentDurationSeconds, 0f, 1f);
                // Smooth ease-out: decelerate as it approaches the target.
                // Target tracks the sync formula so the transition is seamless.
                float smoothT = 1f - (1f - t) * (1f - t);
                float descentTarget = GameState.SurfaceState.GlobalMapPosition.y * SyncFactorY + SyncAnchorY;
                theObject.ObjectOffsets.y = _descentStartY + (descentTarget - _descentStartY) * smoothT;

                if (t >= 1f)
                    _isDescending = false;
            }

            bool terrainRecoveryActive = TerrainAvoidanceHelpers.IsTerrainRecoveryActive(theObject);
            if (!_isDescending && !terrainRecoveryActive)
                MotherShipSmallAi.UpdateRamCycle(_ramState, theObject, now, (float)deltaSeconds);

            // Play warning sound when ram warning starts
            if (_ramState.RamTargetLocked && !_warningSoundPlayed)
            {
                _warningSoundPlayed = true;
                PlayWarningSound();
            }

            // Play attack sound when charging starts
            if (_ramState.IsCharging && !_attackSoundPlayed)
            {
                _attackSoundPlayed = true;
                PlayAttackSound(theObject);
            }

            // Reset sound flags when cycle resets
            if (!_ramState.RamTargetLocked)
            {
                _warningSoundPlayed = false;
                _attackSoundPlayed = false;
            }

            UpdateFacingTowardsShip(theObject);

            float maxDelta = MotherShipDifficultySetup.ScaleTurnSpeed(
                RotationDegreesPerSecond,
                GameState.GamePlayState.MotherShipSmallAggression) * (float)deltaSeconds;
            Xrotation = OmegaObjectHelpers.MoveAngleTowards(Xrotation, TargetXrotation, maxDelta);
            Yrotation = OmegaObjectHelpers.MoveAngleTowards(Yrotation, TargetYrotation, maxDelta);
            Zrotation = OmegaObjectHelpers.MoveAngleTowards(Zrotation, TargetZrotation, maxDelta);

            if (theObject.Rotation != null)
            {
                theObject.Rotation.x = Xrotation;
                theObject.Rotation.y = Yrotation;
                theObject.Rotation.z = Zrotation;
            }

            _lastMovementTime = now;

            AnimateWeakSpot(theObject, (float)deltaSeconds);
            UpdateFlash(theObject);
            HitSparkEffects.MoveHitSparks(theObject);

            SyncMovement(theObject);
            TerrainAvoidanceHelpers.ApplyTerrainRecovery(theObject, (float)deltaSeconds);
            SyncToOriginal(theObject);

            return theObject;
        }

        private void UpdateFacingTowardsShip(I3dObject theObject)
        {
            var now = DateTime.Now;
            float updateInterval = MotherShipDifficultySetup.ScaleUpdateInterval(
                DirectionUpdateIntervalSeconds,
                GameState.GamePlayState.MotherShipSmallAggression);
            if ((now - _lastDirectionUpdateTime).TotalSeconds < updateInterval)
                return;
            _lastDirectionUpdateTime = now;

            var wp = theObject.WorldPosition;
            if (wp == null) return;

            // During charging, face the locked target position instead of the live ship
            float dx, dz;
            if (_ramState.IsCharging)
            {
                dx = _ramState.RamTargetWorldPosition.x - wp.x;
                dz = _ramState.RamTargetWorldPosition.z - wp.z;
            }
            else
            {
                var shipPos = GetShipWorldPosition();
                dx = shipPos.x - wp.x;
                dz = shipPos.z - wp.z;
            }

            if (MathF.Abs(dx) < 1f && MathF.Abs(dz) < 1f) return;

            var heading = OmegaObjectHelpers.GetHeadingFromDirection(dx, dz);
            TargetXrotation = heading.X;
            TargetYrotation = heading.Y;
            TargetZrotation = heading.Z;
        }

        private static Vector3 GetShipWorldPosition()
        {
            var map = GameState.SurfaceState.GlobalMapPosition;
            return new Vector3 { x = map.x, y = 0, z = map.z };
        }

        private void SyncMovement(I3dObject theObject)
        {
            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = SyncAnchorY;
            }

            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(theObject, _syncY, SyncFactorY);
        }

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

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured)
                return;

            if (audioPlayer == null || soundRegistry == null)
                return;

            _audio = audioPlayer;
            _soundRegistry = soundRegistry;
            _thudSound = soundRegistry.Get("lazer_thud");
            _explosionSound = soundRegistry.Get("explosion_main");
            if (soundRegistry.TryGet("mothership_engine", out var engineSound))
                _engineSound = engineSound;
            if (soundRegistry.TryGet("mothership_attack", out var attackSound))
                _attackSound = attackSound;
            _audioConfigured = true;
        }

        private const int HullHitsToDestroy = 10;
        private const int VulnerableHitsToDestroy = HullHitsToDestroy / 3;
        private const float ShipRamDamagePercent = 0.05f;

        private void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null)
                return;

            int maxHealth = EnemySetup.GetMotherShipHealth(theObject.ObjectName, GameState.GamePlayState.MotherShipSmallAggression);
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
                StartFlash(theObject, theObject.ImpactStatus.CrashBoxName == "WeakSpot");
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

            StartExplosion(theObject);
            theObject.ImpactStatus.HasCrashed = false;
        }

        private void PlayThudSound(I3dObject theObject)
        {
            if (_audio != null && _thudSound != null)
            {
                var audioPosition = ((OmegaObject3D)theObject).GetAudioPosition();
                _audio.Play(
                    _thudSound,
                    AudioPlayMode.OneShot,
                    new AudioPlayOptions
                    {
                        WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                    });
            }
        }

        private void StartExplosion(I3dObject theObject)
        {
            if (_isExploding)
                return;

            if (_engineInstance != null)
            {
                _engineInstance.Stop(playEndSegment: false);
                _engineInstance = null;
            }

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

        private void PlayExplosionSound(I3dObject theObject)
        {
            if (_audio != null && _explosionSound != null)
            {
                var audioPosition = ((OmegaObject3D)theObject).GetAudioPosition();
                _audio.Play(
                    _explosionSound,
                    AudioPlayMode.OneShot,
                    new AudioPlayOptions
                    {
                        WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                    });
            }
        }

        private void PlayAttackSound(I3dObject theObject)
        {
            if (_audio != null && _attackSound != null)
            {
                var audioPosition = ((OmegaObject3D)theObject).GetAudioPosition();
                _audio.Play(
                    _attackSound,
                    AudioPlayMode.OneShot,
                    new AudioPlayOptions
                    {
                        WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                    });
            }
        }

        private void PlayWarningSound()
        {
            _shipAiVoiceService.TrySpeak(ShipAiVoiceCue.CollisionWarning, _audio, _soundRegistry);
        }

        private void StartFlash(I3dObject theObject, bool isWeakSpotHit = false)
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

            string color = isWeakSpotHit ? WeakSpotFlashColor : FlashColor;
            foreach (var part in theObject.ObjectParts)
                foreach (var tri in part.Triangles)
                    tri.Color = color;

            _isFlashing = true;
            _flashStartTime = DateTime.Now;
        }

        private void UpdateFlash(I3dObject theObject)
        {
            if (!_isFlashing || _originalColors == null)
                return;

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

        public void ReleaseParticles(I3dObject theObject)
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        private void AnimateWeakSpot(I3dObject theObject, float deltaSeconds)
        {
            var weakSpot = theObject.ObjectParts.Find(p => p.PartName == "MotherShipWeakSpot");
            if (weakSpot?.Triangles == null || weakSpot.Triangles.Count == 0) return;

            if (_weakSpotOriginalTris == null)
            {
                _weakSpotOriginalTris = new List<ITriangleMeshWithColor>(weakSpot.Triangles.Count);
                foreach (var tri in weakSpot.Triangles)
                {
                    _weakSpotOriginalTris.Add(new TriangleMeshWithColor
                    {
                        Color = tri.Color,
                        noHidden = tri.noHidden,
                        angle = tri.angle,
                        vert1 = new Vector3 { x = tri.vert1.x, y = tri.vert1.y, z = tri.vert1.z },
                        vert2 = new Vector3 { x = tri.vert2.x, y = tri.vert2.y, z = tri.vert2.z },
                        vert3 = new Vector3 { x = tri.vert3.x, y = tri.vert3.y, z = tri.vert3.z }
                    });
                }
            }

            float frameScale = deltaSeconds * GameState.GameplayBaselineFps;
            _weakSpotAngle += WeakSpotSpinSpeed * frameScale;
            _pulsatePhase += PulsateSpeed * frameScale;
            float scale = 1f + PulsateAmplitude * MathF.Sin(_pulsatePhase);

            float rad = MathF.PI * _weakSpotAngle / 180f;
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);

            var result = new List<ITriangleMeshWithColor>(_weakSpotOriginalTris.Count);
            foreach (var src in _weakSpotOriginalTris)
            {
                var tri = new TriangleMeshWithColor
                {
                    Color = src.Color,
                    noHidden = src.noHidden
                };

                ApplySpinAndPulsate(src.vert1, tri.vert1, cos, sin, scale);
                ApplySpinAndPulsate(src.vert2, tri.vert2, cos, sin, scale);
                ApplySpinAndPulsate(src.vert3, tri.vert3, cos, sin, scale);

                result.Add(tri);
            }

            weakSpot.Triangles = result;
        }

        private static void ApplySpinAndPulsate(IVector3 src, IVector3 dst, float cos, float sin, float scale)
        {
            float dx = src.x - WeakSpotCenterX;
            float dy = src.y - WeakSpotCenterY;
            float rx = WeakSpotCenterX + dx * cos - dy * sin;
            float ry = WeakSpotCenterY + dy * cos + dx * sin;
            float rz = src.z;

            dst.x = WeakSpotCenterX + (rx - WeakSpotCenterX) * scale;
            dst.y = WeakSpotCenterY + (ry - WeakSpotCenterY) * scale;
            dst.z = WeakSpotCenterZ + (rz - WeakSpotCenterZ) * scale;
        }

        public void Dispose()
        {
            _syncInitialized = false;
            _syncY = 0;
            _weakSpotOriginalTris = null;
            _weakSpotAngle = 0f;
            _pulsatePhase = 0f;
            _lastMovementTime = DateTime.MinValue;
            _lastDirectionUpdateTime = DateTime.MinValue;
            TargetXrotation = BaseXRotation;
            TargetYrotation = BaseYRotation;
            TargetZrotation = BaseZRotation;
            Xrotation = BaseXRotation;
            Yrotation = BaseYRotation;
            Zrotation = BaseZRotation;
            StartCoordinates = null;
            GuideCoordinates = null;
            _audioConfigured = false;
            _isExploding = false;
            _secondExplosionTriggered = false;
            _shipCollisionCooldown = DateTime.MinValue;
            _isFlashing = false;
            _flashStartTime = DateTime.MinValue;
            _originalColors = null;
            _explosionWorldPosition = null;
            _explosionObjectOffsets = null;
            _audio = null;
            _soundRegistry = null;
            _thudSound = null;
            _explosionSound = null;
            if (_engineInstance != null)
            {
                _engineInstance.Stop(playEndSegment: false);
                _engineInstance = null;
            }
            _engineSound = null;
            _attackSound = null;
            _attackSoundPlayed = false;
            _warningSoundPlayed = false;
            MotherShipSmallAi.ResetState(_ramState);
        }
    }
}
