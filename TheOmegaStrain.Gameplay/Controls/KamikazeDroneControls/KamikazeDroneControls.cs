using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls
{
    public class KamikazeDroneControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();
        public DateTime? StartHuntDateTime { get; set; }

        //Initial rotation angles for the drone, pointing towards the camera. Adjust as needed based on the drone model's default orientation.
        private float Yrotation = 0;
        private float Xrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private float Zrotation = 90;
        private float TargetYrotation = 0;
        private float TargetXrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private float TargetZrotation = 90;

        const int DroneSpeedScreenPrSecond = 3; //How many seconds it should take for the drone to cross the entire screen at its current speed. Adjust as needed.
        private const float RotationDegreesPerSecond = 180f;
        private const float DirectionUpdateIntervalSeconds = 1f;
        private const float OvershootSeconds = 5f / GameState.GameplayBaselineFps;
        private const float DroneFlyingVolumeBoost = 1.15f;
        private DateTime LastDirectionUpdateDateTime = DateTime.MinValue;
        private DateTime LastMovementDateTime = DateTime.MinValue;
        private IVector3 DirectionVelocity = new Vector3 { x = 0, y = 0, z = 0 }; // Initially standing still until the first direction is calculated
        private bool _syncInitialized = false;
        private float _syncY = 0;
        private int _trackedObjectId = -1;
        private bool _storedWorldPositionInitialized = false;
        private Vector3 _storedWorldPosition = new Vector3();
        private bool _audioConfigured = false;
        private bool _isExploding = false;
        private DateTime _explosionDeltaTime = DateTime.Now;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;
        private int _surpriseDroneObjectId = -1;
        private bool _surpriseDroneInitialized = false;
        private bool _isSurpriseDrone = false;
        private bool _surpriseDelayInitialized = false;
        private DateTime? _surpriseStartHuntDateTime;
        private IAudioPlayer? _audio;
        private SoundDefinition? _explosionSound;
        private SoundDefinition? _droneFlyingSound;
        private IAudioInstance? _droneFlyingInstance;
        private float _overshootSecondsRemaining = 0f;
        private Vector3 _overshootDirection = new Vector3();

        public KamikazeDroneControls()
        {
            var rd = new Random();
            var TimeDelay = GameSetup.KamikazeDroneMinHuntDelay +
                rd.Next(0, GameSetup.KamikazeDroneMaxHuntDelay - GameSetup.KamikazeDroneMinHuntDelay);
            // Delay the start of the hunt to give the player some time to react before the drone starts moving, and to help desync multiple drones if they spawn at similar times.
            StartHuntDateTime = DateTime.Now.AddSeconds(TimeDelay);
        }

        private void UpdateRotationTowardsTarget(Vector3 directionToTarget)
        {
            var heading = OmegaObjectHelpers.GetHeadingFromDirection(directionToTarget.x, directionToTarget.z);
            TargetXrotation = heading.X;
            TargetYrotation = heading.Y;
            TargetZrotation = heading.Z;
        }

        private void AlignRotationToDirection(Vector3 movementDirection)
        {
            UpdateRotationTowardsTarget(movementDirection);
            Xrotation = TargetXrotation;
            Yrotation = TargetYrotation;
            Zrotation = TargetZrotation;
        }

        private void UpdateCurrentRotation(double deltaSeconds)
        {
            float maxDelta = RotationDegreesPerSecond * (float)deltaSeconds;
            if (maxDelta <= 0f)
            {
                return;
            }

            Xrotation = OmegaObjectHelpers.MoveAngleTowards(Xrotation, TargetXrotation, maxDelta);
            Yrotation = OmegaObjectHelpers.MoveAngleTowards(Yrotation, TargetYrotation, maxDelta);
            Zrotation = OmegaObjectHelpers.MoveAngleTowards(Zrotation, TargetZrotation, maxDelta);
        }

        private void SyncMovement(I3dObject theObject)
        {
            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = theObject.ObjectOffsets?.y ?? 0f;
            }

            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(theObject, _syncY);
        }

        private void HandleCrash(I3dObject theObject)
        {
            if (_droneFlyingInstance != null)
            {
                _droneFlyingInstance.Stop(playEndSegment: false);
                _droneFlyingInstance = null;
            }

            if (theObject.ImpactStatus == null)
            {
                return;
            }

            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? EnemySetup.KamikazeDroneHealth;
            int damage = theObject.ImpactStatus.ObjectName switch
            {
                "Ship" => EnemySetup.KamikazeDroneHealth,
                string objectName when WeaponSetup.IsWeaponTypeValid(objectName) => WeaponSetup.GetWeaponDamage(objectName),
                _ => currentHealth
            };

            theObject.ImpactStatus.ObjectHealth = currentHealth - damage;

            if (theObject.ImpactStatus.ObjectHealth > 0)
            {
                HitSparkEffects.ReleaseHitSparks(theObject, this, theObject.ImpactStatus.ObjectName);
                theObject.ImpactStatus.HasCrashed = false;
                return;
            }

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

            _isExploding = true;
            _explosionDeltaTime = DateTime.Now;
            _explosionWorldPosition = theObject.WorldPosition as Vector3 ?? KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
            _explosionObjectOffsets = theObject.ObjectOffsets as Vector3 ?? KamikazeDroneMovementHelpers.ToVector3(theObject.ObjectOffsets);

            ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
            Physics.ExplodeObject(theObject, 200f);
            theObject.CrashBoxes = new List<List<IVector3>>();
            theObject.ImpactStatus.HasCrashed = false;
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            var now = DateTime.Now;

            // Skip hunt until the delay expires, unless the ship is already close
            if (StartHuntDateTime != null &&
                now < StartHuntDateTime &&
                HasLiveNonDroneEnemies() &&
                IsShipOutsideImmediateHuntRange(theObject))
            {
                return theObject;
            }

            if (ShouldWaitForSurpriseHuntDelay(theObject, now))
            {
                return theObject;
            }

            ConfigureAudio(audioPlayer, soundRegistry);

            if (_trackedObjectId != theObject.ObjectId)
            {
                _trackedObjectId = theObject.ObjectId;
                _storedWorldPosition = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
                _storedWorldPositionInitialized = theObject.WorldPosition != null;
                _overshootSecondsRemaining = 0f;
                _overshootDirection = new Vector3();
            }
            else if (_storedWorldPositionInitialized)
            {
                theObject.WorldPosition = new Vector3
                {
                    x = _storedWorldPosition.x,
                    y = _storedWorldPosition.y,
                    z = _storedWorldPosition.z
                };
            }

            ParentObject = theObject;
            SyncMovement(theObject);
            TerrainAvoidanceHelpers.TryStartTerrainRecovery(theObject);

            if (theObject.ImpactStatus?.HasCrashed == true && !_isExploding)
            {
                HandleCrash(theObject);
            }

            if (_audio != null && _droneFlyingSound != null && !_isExploding)
            {
                if (theObject.IsOnScreen)
                {
                    var audioPosition = ((OmegaObject3D)theObject).GetAudioPosition();

                    if (_droneFlyingInstance == null || !_droneFlyingInstance.IsPlaying)
                    {
                        _droneFlyingInstance = _audio.Play(
                            _droneFlyingSound,
                            AudioPlayMode.SegmentedLoop,
                            new AudioPlayOptions
                            {
                                WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                            });
                    }

                    _droneFlyingInstance.SetVolume(GetDroneFlyingVolume());
                    _droneFlyingInstance.SetWorldPosition(new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z));
                }
                else
                {
                    var globalPos = GameState.SurfaceState?.GlobalMapPosition;
                    var droneWorldPos = theObject.WorldPosition;

                    if (globalPos != null && droneWorldPos != null)
                    {
                        float distSq = OmegaObjectHelpers.GetDistanceSquared(globalPos, droneWorldPos);
                        float maxDist = AudioSetup.OffscreenAiAudioMaxDistance;
                        float maxDistSq = maxDist * maxDist;

                        if (distSq <= maxDistSq)
                        {
                            float distance = MathF.Sqrt(distSq);
                            float normalized = distance / maxDist;
                            float volume = GetDroneFlyingVolume() *
                                MathF.Pow(1f - normalized, AudioSetup.OffscreenAiAudioCurveExponent);

                            if (_droneFlyingInstance == null || !_droneFlyingInstance.IsPlaying)
                            {
                                _droneFlyingInstance = _audio.Play(
                                    _droneFlyingSound,
                                    AudioPlayMode.SegmentedLoop,
                                    new AudioPlayOptions
                                    {
                                        WorldPosition = System.Numerics.Vector3.Zero
                                    });
                            }

                            float dx = droneWorldPos.x - globalPos.x;
                            _droneFlyingInstance.SetWorldPosition(new System.Numerics.Vector3(dx, 0, 0));
                            _droneFlyingInstance.SetVolume(volume);
                        }
                        else if (_droneFlyingInstance != null)
                        {
                            _droneFlyingInstance.Stop(playEndSegment: false);
                            _droneFlyingInstance = null;
                        }
                    }
                    else if (_droneFlyingInstance != null)
                    {
                        _droneFlyingInstance.Stop(playEndSegment: false);
                        _droneFlyingInstance = null;
                    }
                }
            }

            if (_isExploding)
            {
                if (_explosionWorldPosition != null)
                {
                    theObject.WorldPosition = _explosionWorldPosition;
                }

                if (_explosionObjectOffsets != null)
                {
                    theObject.ObjectOffsets = _explosionObjectOffsets;
                }

                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                ExplosionParticleHelpers.MoveParticles(theObject);
                if (theObject.ImpactStatus?.HasExploded == true)
                {
                    theObject.ObjectParts = new List<I3dObjectPart>();
                }

                _storedWorldPosition = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
                _storedWorldPositionInitialized = theObject.WorldPosition != null;
                KamikazeDroneAi.SyncAuthoritativeDroneState(theObject);
                LastMovementDateTime = DateTime.Now;
                return theObject;
            }

            if (LastMovementDateTime == DateTime.MinValue)
            {
                LastMovementDateTime = now;
            }

            // Drones stay idle until the player unlocks the Decoy powerup
            if (!GameState.GamePlayState.IsDecoyUnlocked)
            {
                TerrainAvoidanceHelpers.ApplyTerrainRecovery(theObject, GameState.ClampedDeltaTime);
                _storedWorldPosition = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
                _storedWorldPositionInitialized = theObject.WorldPosition != null;
                KamikazeDroneAi.SyncAuthoritativeDroneState(theObject);
                LastMovementDateTime = now;
                return theObject;
            }

            var deltaSeconds = (now - LastMovementDateTime).TotalSeconds;

            IVector3? currentDronePosition = null;
            IVector3? currentTargetPosition = null;
            Vector3 directionToTarget = new Vector3();
            bool shouldRecalculateDirection = false;
            bool isOvershooting = _overshootSecondsRemaining > 0f;
            float speedPerSecond = 0f;

            var closestDecoy = KamikazeDroneAi.GetClosestActiveDecoy(ParentObject);
            Vector3? targetWorldPosition = closestDecoy != null
                ? KamikazeDroneMovementHelpers.GetCompensatedHuntTargetWorldPosition(ParentObject, closestDecoy)
                : KamikazeDroneMovementHelpers.GetShipCrashCenterWorldPosition();

            if (ParentObject.WorldPosition is IVector3 && targetWorldPosition is Vector3 resolvedTargetWorldPosition)
            {
                var parentWorldPosition = closestDecoy != null
                    ? KamikazeDroneMovementHelpers.GetNavigationCrashCenterWorldPosition(ParentObject)
                    : KamikazeDroneMovementHelpers.GetDroneCrashCenterWorldPosition(ParentObject);
                currentDronePosition = parentWorldPosition;
                currentTargetPosition = resolvedTargetWorldPosition;

                directionToTarget = new Vector3
                {
                    x = resolvedTargetWorldPosition.x - parentWorldPosition.x,
                    y = resolvedTargetWorldPosition.y - parentWorldPosition.y,
                    z = resolvedTargetWorldPosition.z - parentWorldPosition.z
                };

                if (isOvershooting)
                {
                    directionToTarget = _overshootDirection;
                }

                // Use the same direction vector as movement so heading and travel are always consistent.
                var headingDirection = isOvershooting
                    ? directionToTarget
                    : new Vector3
                    {
                        x = directionToTarget.x,
                        y = 0f,
                        z = directionToTarget.z
                    };
                UpdateRotationTowardsTarget(headingDirection);

                var distance = OmegaObjectHelpers.GetDistance(parentWorldPosition, resolvedTargetWorldPosition);

                shouldRecalculateDirection = !isOvershooting &&
                    (LastDirectionUpdateDateTime == DateTime.MinValue ||
                    (now - LastDirectionUpdateDateTime).TotalSeconds >= DirectionUpdateIntervalSeconds ||
                    KamikazeDroneMovementHelpers.Dot((Vector3)DirectionVelocity, directionToTarget) <= 0f);

                if (shouldRecalculateDirection && distance > 0)
                {
                    var speed = TheOmegaStrain.Common.CommonSetup.ScreenSetup.screenSizeX / (float)DroneSpeedScreenPrSecond;

                    DirectionVelocity = new Vector3
                    {
                        x = directionToTarget.x / (float)distance * speed,
                        y = directionToTarget.y / (float)distance * speed,
                        z = directionToTarget.z / (float)distance * speed
                    };
                    LastDirectionUpdateDateTime = now;
                }
                else if (distance <= 0)
                {
                    DirectionVelocity = new Vector3 { x = 0, y = 0, z = 0 };
                }
            }

            if (theObject.WorldPosition is IVector3 objectWorldPosition)
            {
                currentDronePosition = closestDecoy != null
                    ? KamikazeDroneMovementHelpers.GetNavigationCrashCenterWorldPosition(theObject)
                    : KamikazeDroneMovementHelpers.GetDroneCrashCenterWorldPosition(theObject);

                if (currentDronePosition is Vector3 dronePosition && currentTargetPosition is Vector3 targetPosition)
                {
                    var liveDirectionToTarget = new Vector3
                    {
                        x = targetPosition.x - dronePosition.x,
                        y = targetPosition.y - dronePosition.y,
                        z = targetPosition.z - dronePosition.z
                    };

                    float currentDistance = (float)OmegaObjectHelpers.GetDistance(dronePosition, targetPosition);
                    speedPerSecond = KamikazeDroneMovementHelpers.Length((Vector3)DirectionVelocity);

                    if (speedPerSecond > 0f)
                    {
                        Vector3 moveDirection;
                        float moveDistance = speedPerSecond * (float)deltaSeconds;

                        if (_overshootSecondsRemaining > 0f)
                        {
                            moveDirection = KamikazeDroneMovementHelpers.Normalize(_overshootDirection);
                            _overshootSecondsRemaining = MathF.Max(0f, _overshootSecondsRemaining - (float)deltaSeconds);
                        }
                        else
                        {
                            moveDirection = KamikazeDroneMovementHelpers.Normalize(liveDirectionToTarget);

                            if (currentDistance > 0f && moveDistance >= currentDistance)
                            {
                                _overshootDirection = moveDirection;
                                _overshootSecondsRemaining = OvershootSeconds;
                            }
                            else if (currentDistance <= 0f)
                            {
                                moveDistance = 0f;
                            }
                            else
                            {
                                moveDistance = MathF.Min(moveDistance, currentDistance);
                            }
                        }

                        if (moveDistance > 0f)
                        {
                            AlignRotationToDirection(moveDirection);
                        }

                        theObject.WorldPosition = new Vector3
                        {
                            x = objectWorldPosition.x + (moveDirection.x * moveDistance),
                            y = objectWorldPosition.y + (moveDirection.y * moveDistance),
                            z = objectWorldPosition.z + (moveDirection.z * moveDistance)
                        };
                    }
                    else
                    {
                        theObject.WorldPosition = new Vector3
                        {
                            x = objectWorldPosition.x,
                            y = objectWorldPosition.y,
                            z = objectWorldPosition.z
                        };
                    }
                }

                currentDronePosition = closestDecoy != null
                    ? KamikazeDroneMovementHelpers.GetNavigationCrashCenterWorldPosition(theObject)
                    : KamikazeDroneMovementHelpers.GetDroneCrashCenterWorldPosition(theObject);

                if (closestDecoy != null && currentDronePosition is Vector3 dronePositionAfterMove)
                {
                    var decoyCenter = KamikazeDroneMovementHelpers.GetCompensatedHuntTargetWorldPosition(theObject, closestDecoy);
                    float touchDistance = KamikazeDroneMovementHelpers.GetApproximateCrashRadius(theObject) + KamikazeDroneMovementHelpers.GetApproximateCrashRadius(closestDecoy);
                    float currentDistanceToDecoy = (float)OmegaObjectHelpers.GetDistance(dronePositionAfterMove, decoyCenter);

                    if (currentDistanceToDecoy <= touchDistance)
                    {
                        KamikazeDroneAi.TriggerDecoyCollision(theObject, closestDecoy);
                    }
                }
            }

            UpdateCurrentRotation(deltaSeconds);

            if (theObject.Rotation != null)
            {
                var rotation = theObject.Rotation;
                rotation.y = Yrotation;
                rotation.x = Xrotation;
                rotation.z = Zrotation;
            }

            TerrainAvoidanceHelpers.ApplyTerrainRecovery(theObject, (float)deltaSeconds);
            HitSparkEffects.MoveHitSparks(theObject);
            _storedWorldPosition = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
            _storedWorldPositionInitialized = theObject.WorldPosition != null;
            KamikazeDroneAi.SyncAuthoritativeDroneState(theObject);
            LastMovementDateTime = now;

            return theObject;
        }

        private static bool HasLiveNonDroneEnemies()
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null || aiObjects.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var aiObject = aiObjects[i];
                if (aiObject.ImpactStatus?.HasExploded == true)
                {
                    continue;
                }

                if (!EnemySetup.IsEnemyTypeValid(aiObject.ObjectName))
                {
                    continue;
                }

                if (IsEssentialNonDroneEnemy(aiObject.ObjectName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsEssentialNonDroneEnemy(string objectName)
        {
            return objectName == "Seeder"
                || objectName == "MotherShipSmall"
                || objectName == "MotherShipMedium"
                || objectName == "MotherShipLarge"
                || objectName == "AttackShip"
                || objectName == "Endboss1"
                || objectName == "Endboss2";
        }

        private bool ShouldWaitForSurpriseHuntDelay(I3dObject theObject, DateTime now)
        {
            if (!GameState.GamePlayState.IsDecoyUnlocked)
            {
                return false;
            }

            if (!IsSurpriseDelayScene())
            {
                return false;
            }

            if (!IsSurpriseDrone(theObject))
            {
                return false;
            }

            if (!_surpriseDelayInitialized)
            {
                _surpriseDelayInitialized = true;
                _surpriseStartHuntDateTime = now.AddSeconds(GameSetup.KamikazeDroneSurpriseHuntDelaySeconds);
            }

            return now < _surpriseStartHuntDateTime && IsShipOutsideImmediateHuntRange(theObject);
        }

        private bool IsSurpriseDrone(I3dObject theObject)
        {
            if (_surpriseDroneObjectId != theObject.ObjectId)
            {
                _surpriseDroneObjectId = theObject.ObjectId;
                _surpriseDroneInitialized = false;
                _isSurpriseDrone = false;
                _surpriseDelayInitialized = false;
                _surpriseStartHuntDateTime = null;
            }

            if (!_surpriseDroneInitialized)
            {
                _isSurpriseDrone = ShouldUseSurpriseDelay(theObject);
                _surpriseDroneInitialized = true;
            }

            return _isSurpriseDrone;
        }

        private static bool ShouldUseSurpriseDelay(I3dObject theObject)
        {
            if (theObject.ObjectName != "KamikazeDrone")
            {
                return false;
            }

            int percent = Math.Clamp(GameSetup.KamikazeDroneSurpriseDelayPercent, 0, 100);
            if (percent <= 0)
            {
                return false;
            }

            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null || aiObjects.Count == 0)
            {
                return false;
            }

            int droneCount = 0;
            int droneIndex = -1;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectName != "KamikazeDrone")
                {
                    continue;
                }

                if (aiObjects[i].ObjectId == theObject.ObjectId)
                {
                    droneIndex = droneCount;
                }

                droneCount++;
            }

            if (droneIndex < 0 || droneCount <= 0)
            {
                return false;
            }

            int surpriseCount = Math.Max(1, (droneCount * percent + 99) / 100);
            return droneIndex >= droneCount - surpriseCount;
        }

        private static bool IsSurpriseDelayScene()
        {
            var sceneType = GameState.GamePlayState.CurrentSceneType;
            return sceneType == SceneTypes.Game || sceneType == SceneTypes.Simulation;
        }

        private static bool IsShipOutsideImmediateHuntRange(I3dObject theObject)
        {
            var shipPos = GameState.ShipState?.ShipCrashCenterWorldPosition;
            if (shipPos == null || theObject.WorldPosition == null)
            {
                return true;
            }

            float distToShip = (float)OmegaObjectHelpers.GetDistance(
                KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition),
                KamikazeDroneMovementHelpers.ToVector3(shipPos));

            return distToShip > GameSetup.KamikazeDroneProximityHuntDistance;
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void Dispose()
        {
            if (_droneFlyingInstance != null)
            {
                _droneFlyingInstance.Stop(playEndSegment: false);
                _droneFlyingInstance = null;
            }

            _audioConfigured = false;
            _isExploding = false;
            _syncInitialized = false;
            _syncY = 0;
            _trackedObjectId = -1;
            _storedWorldPositionInitialized = false;
            _storedWorldPosition = new Vector3();
            _surpriseDroneObjectId = -1;
            _surpriseDroneInitialized = false;
            _isSurpriseDrone = false;
            _surpriseDelayInitialized = false;
            _surpriseStartHuntDateTime = null;
            _overshootSecondsRemaining = 0f;
            _overshootDirection = new Vector3();
            _explosionWorldPosition = null;
            _explosionObjectOffsets = null;
            StartCoordinates = null;
            GuideCoordinates = null;
            DirectionVelocity = new Vector3 { x = 0, y = 0, z = 0 };
            LastDirectionUpdateDateTime = DateTime.MinValue;
            LastMovementDateTime = DateTime.MinValue;
            _audio = null;
            _explosionSound = null;
            _droneFlyingSound = null;
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            throw new NotImplementedException();
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured)
            {
                return;
            }

            if (audioPlayer == null || soundRegistry == null)
            {
                return;
            }

            _audio = audioPlayer;
            _explosionSound = soundRegistry.Get("explosion_main");
            if (soundRegistry.TryGet("drone_flying", out var droneFlyingSound))
            {
                _droneFlyingSound = droneFlyingSound;
            }
            else if (soundRegistry.TryGet("drone_coming", out var droneComingSound))
            {
                _droneFlyingSound = droneComingSound;
            }
            _audioConfigured = true;
        }

        private float GetDroneFlyingVolume()
        {
            return (_droneFlyingSound?.Settings.Volume ?? 1f) * DroneFlyingVolumeBoost;
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            throw new NotImplementedException();
        }
    }
}
