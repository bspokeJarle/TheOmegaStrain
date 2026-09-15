using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class MedKitPickupControls : IObjectMovement
    {
        // Visual rotation. Up is the vertical axis and Z goes into the screen. The mesh is
        // authored Z-up locally, so a fixed 90deg tilt about X lifts the lid (+Z local) to
        // point straight up. Rotations are applied Z -> Y -> X, which makes the world spin
        // axis coincide with the lid normal: the Z rotation (applied first) then spins the
        // lid in place, and any extra lean added to X tilts the lid AND its spin axis
        // together, so the lid holds a constant lean toward the camera without wobbling.
        private const float CameraLeanDegrees = -30f;   // tip the lid toward the camera
        private const float BaseXRotation = 90f + CameraLeanDegrees;
        private const float BaseYRotation = 0f;
        private const float BaseZRotation = 0f;
        private const float BaseZRotationIncrementPerFrame = 0.8f;

        // Sync offsets:
        // - SyncFactorY: scales how much the Y-offset follows the surface's GlobalMapPosition.y.
        private const float SyncFactorY = 2.5f;

        // World-space follow toward the ship, using the same crash-center targeting the
        // KamikazeDrone uses: the medkit lives at a real WorldPosition and chases the ship
        // through the world (it is NOT screen-anchored). The pull speed ramps up the closer
        // it gets, so it snaps in like a magnet, then eases to a stop just short of the ship.
        private const float FollowRampDistance = 700f;        // within this, the pull ramps toward max (world units ~ px)
        private const float FollowStopDistance = 40f;         // close enough; hover here without jitter
        private const float FollowMinSpeedPerSecond = 300f;   // pull speed when far away
        private const float FollowMaxSpeedPerSecond = 600f;  // pull speed right next to the ship
        private const float FollowMaxDeltaSeconds = 0.1f;     // clamp frame gaps (pause / first frame)

        // Subtle vertical bob. Up is toward -Y, so subtracting from Y raises the med-kit.
        private const float BobAmplitude = 9f;               // screen px
        private const float BobSpeedPerFrame = 0.11f;        // radians per 90fps frame

        // Diagnostics:
        private const bool enableLogging = false;
        private const int LogEveryNthFrame = 60;

        // Explosion:
        private const float ExplosionForce = 100f;

        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        private float Yrotation = BaseYRotation;
        private float Zrotation = BaseZRotation;
        private float Xrotation = BaseXRotation;

        private bool _syncInitialized = false;
        private float _syncY = 0;
        private int _logFrameCounter = 0;

        private DateTime _lastFollowTime = DateTime.Now;
        private float _bobPhase = 0f;
        private int _trackedObjectId = -1;
        private bool _storedWorldPositionInitialized = false;
        private Vector3 _storedWorldPosition = new Vector3();

        // Spawn zoom-in animation
        private bool _spawnAnimating = true;
        private float _spawnFrame = 0f;
        private const int SpawnAnimationFrames = 45;
        private const float SpawnStartZExtra = 800f;
        private float _spawnTargetZ;
        private bool _spawnTargetCaptured = false;
        private List<List<IVector3>>? _savedCrashBoxes;

        private bool _isExploding = false;
        private DateTime _explosionDeltaTime = DateTime.Now;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;

        private static void SafeLog(string message)
        {
            try
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log(message, "PowerUp");
            }
            catch { }
        }

        private static string FormatVector(Vector3 v)
        {
            return string.Create(CultureInfo.InvariantCulture, $"x={v.x:0.##};y={v.y:0.##};z={v.z:0.##}");
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            if (theObject.ImpactStatus?.HasExploded == true)
                return theObject;

            // Handle collision: the ship's power-up pipeline (CollectPowerUp) applies the heal;
            // here we just play the disappear animation. No explosion sound — the pickup sound
            // is played by ShipControls, exactly like the standard power-up.
            if (theObject.ImpactStatus?.HasCrashed == true && !_isExploding)
            {
                SafeLog($"[MedKit] HasCrashed=true, ObjectName='{theObject.ImpactStatus.ObjectName}', collected. Id={theObject.ObjectId}");
                _isExploding = true;
                _explosionDeltaTime = DateTime.Now;
                _explosionWorldPosition = CopyVector(theObject.WorldPosition);
                _explosionObjectOffsets = CopyVector(theObject.ObjectOffsets);
                Physics.ExplosionColorOverride = "3CE86A";   // green burst reads as healing
                ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
                Physics.ExplodeObject(theObject, ExplosionForce);
                RestoreExplosionTransform(theObject);
                theObject.CrashBoxes = new List<List<IVector3>>();
            }

            if (_isExploding)
            {
                RestoreExplosionTransform(theObject);

                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                RestoreExplosionTransform(theObject);
                ExplosionParticleHelpers.MoveParticles(theObject);
                if (theObject.ImpactStatus?.HasExploded == true)
                    theObject.ObjectParts = new List<I3dObjectPart>();

                SyncToOriginal(theObject);
                return theObject;
            }

            if (theObject.Rotation != null) theObject.Rotation.y = Yrotation;
            if (theObject.Rotation != null) theObject.Rotation.x = Xrotation;
            if (theObject.Rotation != null) theObject.Rotation.z = Zrotation;

            // Visual spin around the vertical (up) axis; keeps the lid pointing straight up
            Zrotation += BaseZRotationIncrementPerFrame * GameState.FrameScale90;

            // Keep offsets visually in sync with surface scrolling
            SyncMovement(theObject);

            // Spawn zoom-in: start high and ease down to target Z
            if (_spawnAnimating)
            {
                if (!_spawnTargetCaptured)
                {
                    _spawnTargetZ = theObject.ObjectOffsets.z;
                    _savedCrashBoxes = theObject.CrashBoxes;
                    theObject.CrashBoxes = new List<List<IVector3>>();
                    _spawnTargetCaptured = true;
                }

                _spawnFrame += GameState.FrameScale90;
                float t = Math.Min(1f, (float)_spawnFrame / SpawnAnimationFrames);
                float eased = 1f - (1f - t) * (1f - t);
                theObject.ObjectOffsets.z = _spawnTargetZ + SpawnStartZExtra * (1f - eased);

                if (t >= 1f)
                {
                    _spawnAnimating = false;
                    theObject.ObjectOffsets.z = _spawnTargetZ;
                    theObject.CrashBoxes = _savedCrashBoxes;
                }
            }

            // World-space follow. The medkit lives at a real WorldPosition and chases the
            // ship through the world (see the follow constants above), the same way the
            // KamikazeDrone hunts. We keep our own authoritative WorldPosition and restore it
            // each frame before stepping it, mirroring the drone's store/restore pattern.
            var now = DateTime.Now;
            float followDeltaSeconds = MathF.Min(FollowMaxDeltaSeconds, (float)(now - _lastFollowTime).TotalSeconds);
            _lastFollowTime = now;

            if (_trackedObjectId != theObject.ObjectId)
            {
                _trackedObjectId = theObject.ObjectId;
                _storedWorldPosition = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
                _storedWorldPositionInitialized = theObject.WorldPosition != null;
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

            var shipTarget = KamikazeDroneMovementHelpers.GetShipCrashCenterWorldPosition();
            if (!_spawnAnimating
                && shipTarget is Vector3 target
                && theObject.WorldPosition is IVector3 medkitWorld)
            {
                var medkitCenter = KamikazeDroneMovementHelpers.GetDroneCrashCenterWorldPosition(theObject);
                var toTarget = MovementHelpers.GetVectorToTarget(medkitCenter, target);
                float distance = MovementHelpers.GetLength(toTarget);

                if (distance > FollowStopDistance)
                {
                    // Proximity: 0 far away, 1 right next to the ship. Squaring makes the
                    // pull accelerate as the gap closes (magnet feel).
                    float proximity = Math.Clamp(1f - (distance / FollowRampDistance), 0f, 1f);
                    float speedPerSecond = FollowMinSpeedPerSecond
                        + (FollowMaxSpeedPerSecond - FollowMinSpeedPerSecond) * (proximity * proximity);

                    float step = MathF.Min(speedPerSecond * followDeltaSeconds, distance - FollowStopDistance);
                    var direction = new Vector3
                    {
                        x = toTarget.x / distance,
                        y = toTarget.y / distance,
                        z = toTarget.z / distance
                    };
                    theObject.WorldPosition = MovementHelpers.MoveAlongDirection(medkitWorld, direction, step);
                }
            }

            _storedWorldPosition = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
            _storedWorldPositionInitialized = theObject.WorldPosition != null;

            theObject.ObjectOffsets.y -= BobAmplitude * MathF.Sin(_bobPhase);
            _bobPhase += BobSpeedPerFrame * GameState.FrameScale90;

            SurfacePositionSyncHelpers.AddSurfacePitchHeightCorrectionY(
                theObject,
                WorldViewSetup.SurfacePitchDegrees);

            // Push positions back to original in AiObjects
            SyncToOriginal(theObject);

            if (Logger.ShouldLog(enableLogging))
            {
                _logFrameCounter++;
                if (_logFrameCounter % LogEveryNthFrame == 0)
                {
                    var offsets = theObject.ObjectOffsets != null ? FormatVector((Vector3)theObject.ObjectOffsets) : "null";
                    var world = theObject.WorldPosition != null ? FormatVector((Vector3)theObject.WorldPosition) : "null";
                    int crashBoxCount = theObject.CrashBoxes?.Count ?? 0;
                    SafeLog($"[PowerUp] Id={theObject.ObjectId} Offsets={offsets} World={world} CrashBoxes={crashBoxCount} HasCrashed={theObject.ImpactStatus?.HasCrashed} HasExploded={theObject.ImpactStatus?.HasExploded}");
                }
            }

            return theObject;
        }

        private void SyncMovement(I3dObject theObject)
        {
            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = theObject.ObjectOffsets.y;
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

        private void RestoreExplosionTransform(I3dObject theObject)
        {
            if (_explosionWorldPosition != null)
                theObject.WorldPosition = CopyVector(_explosionWorldPosition);

            if (_explosionObjectOffsets != null)
                theObject.ObjectOffsets = CopyVector(_explosionObjectOffsets);
        }

        private static Vector3 CopyVector(IVector3 source)
        {
            return new Vector3
            {
                x = source.x,
                y = source.y,
                z = source.z
            };
        }

        public void ReleaseParticles()
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
        }

        public void Dispose()
        {
            _syncInitialized = false;
            _syncY = 0;
            _spawnAnimating = true;
            _spawnFrame = 0f;
            _spawnTargetCaptured = false;
            _savedCrashBoxes = null;
            _isExploding = false;
            _explosionWorldPosition = null;
            _explosionObjectOffsets = null;
            _bobPhase = 0f;
            _trackedObjectId = -1;
            _storedWorldPositionInitialized = false;
            _storedWorldPosition = new Vector3();
            _lastFollowTime = DateTime.Now;
            StartCoordinates = null;
            GuideCoordinates = null;
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
        }

        public void ReleaseParticles(I3dObject theObject)
        {
        }
    }
}
