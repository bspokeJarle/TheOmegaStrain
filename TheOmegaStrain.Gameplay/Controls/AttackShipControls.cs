using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;

namespace TheOmegaStrain.Gameplay.Controls
{
    public sealed class AttackShipControls : IObjectMovement
    {
        // Engines emit a steady stream, alternating frames to keep the particle count sane.
        private const int FramesBetweenReleases = 2;
        private const int ParticleThrust = 3;
        private const float SecondsPerScreenCrossing = 3f;
        private const float MaximumMovementDeltaSeconds = 0.1f;
        private const double PositionLogIntervalSeconds = 1;

        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineStartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineGuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();
        private IAudioPlayer? _audio;
        private SoundDefinition? _explosionSound;
        private bool _isExploding;
        private DateTime _explosionDeltaTime;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;
        private Vector3? _explosionRotation;

        // Controllers survive the engine's per-frame deep copies of scene objects.
        // Keep the authoritative position and captured scene height here.
        private float? _initialOffsetY;
        private DateTime? _lastMovementTime;
        private DateTime _lastPositionLogTime = DateTime.MinValue;

        private int _framesSinceRelease;
        private readonly OmegaMeshRotation _rotate = new();
        private Vector3? _trackedWorldPosition;
        // Compensated target for our world origin, not ShipState's raw world coordinates.
        public Vector3? ShipTargetWorldPosition { get; private set; }
        public Vector3? DirectionToShip { get; private set; }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            ConfigureAudio(audioPlayer, soundRegistry);
            // Handle the collision transform before navigation can move the object.
            if (!_isExploding && theObject.ImpactStatus?.HasCrashed == true)
                HandleCrash(theObject);
            if (_isExploding)
            {
                theObject.WorldPosition = KamikazeDroneMovementHelpers.ToVector3(_explosionWorldPosition);
                theObject.ObjectOffsets = KamikazeDroneMovementHelpers.ToVector3(_explosionObjectOffsets);
                theObject.Rotation = KamikazeDroneMovementHelpers.ToVector3(_explosionRotation);
                theObject.CrashBoxes = new List<List<IVector3>>();
                Physics.UpdateExplosion(theObject, _explosionDeltaTime);
                ExplosionParticleHelpers.MoveParticles(theObject);
                if (theObject.ImpactStatus?.HasExploded == true)
                    theObject.ObjectParts = new List<I3dObjectPart>();
                return theObject;
            }
            var now = DateTime.Now;
            float deltaSeconds = GetMovementDeltaSeconds(now);
            RestoreMovementState(theObject);
            PursueShip(theObject, deltaSeconds);

            // Apply the view correction after navigation and before emitting exhaust.
            SurfacePositionSyncHelpers.AddSurfacePitchHeightCorrectionY(
                theObject, WorldViewSetup.SurfacePitchDegrees);
            SyncAuthoritativeTransform(theObject);
            LogPosition(theObject, now);

            ReleaseParticles(theObject);
            if (theObject.Particles?.Particles.Count > 0)
                theObject.Particles.MoveParticles();

            return theObject;
        }

        private void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null)
            {
                return;
            }

            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? EnemySetup.AttackShipHealth;
            int damage = theObject.ImpactStatus.ObjectName switch
            {
                "Ship" => currentHealth,
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
            _explosionWorldPosition = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
            _explosionObjectOffsets = KamikazeDroneMovementHelpers.ToVector3(theObject.ObjectOffsets);
            _explosionRotation = KamikazeDroneMovementHelpers.ToVector3(theObject.Rotation);

            ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
            Physics.ExplodeObject(theObject, 200f);
            theObject.CrashBoxes = new List<List<IVector3>>();
            theObject.ImpactStatus.HasCrashed = false;
        }

        private float GetMovementDeltaSeconds(DateTime now)
        {
            // Off-screen AI updates are less frequent; measure elapsed time locally.
            float dt = _lastMovementTime.HasValue
                ? Math.Clamp((float)(now - _lastMovementTime.Value).TotalSeconds, 0f, MaximumMovementDeltaSeconds)
                : 0f;
            _lastMovementTime = now;
            return dt;
        }

        private void RestoreMovementState(I3dObject theObject)
        {
            _initialOffsetY ??= theObject.ObjectOffsets.y;

            theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(
                theObject, _initialOffsetY.Value);
            // Capture the scene's placement once, as the other movement controllers do.
            if (_trackedWorldPosition == null && theObject.WorldPosition != null)
                _trackedWorldPosition = new Vector3(
                    theObject.WorldPosition.x, theObject.WorldPosition.y, theObject.WorldPosition.z);
            if (_trackedWorldPosition != null)
                theObject.WorldPosition = new Vector3(
                    _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);

        }

        private void PursueShip(I3dObject theObject, float deltaSeconds)
        {
            // Pursue the world origin that aligns our rendered crash centre with Ship.
            // The shared helper accounts for both offsets and the reversed world-Z axis.
            ShipTargetWorldPosition = GameState.ShipState?.ShipCrashCenterWorldPosition == null &&
                GameState.ShipState?.ShipWorldPosition == null
                ? null
                : SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(theObject);
            if (ShipTargetWorldPosition != null && theObject.IsOnScreen)
            {
                // MoveObject adds this correction to ObjectOffsets.y after movement.
                // Include it in the target without applying it twice to the object.
                ShipTargetWorldPosition.y -= SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(
                    theObject, WorldViewSetup.SurfacePitchDegrees);
            }
            DirectionToShip = ShipTargetWorldPosition == null
                ? null
                : MovementHelpers.GetVectorToTarget(
                    KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition),
                    ShipTargetWorldPosition);

            if (ShipTargetWorldPosition != null && _trackedWorldPosition != null)
            {
                var position = KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
                var velocity = MovementHelpers.GetVelocityTowardsTarget(position, ShipTargetWorldPosition,
                    MovementHelpers.GetScreenCrossingSpeed(SecondsPerScreenCrossing));
                var step = MovementHelpers.GetPursuitStep(position, ShipTargetWorldPosition, velocity, deltaSeconds);
                if (step.HasMovement)
                {
                    // Stop at the target instead of stepping past it on a long frame.
                    _trackedWorldPosition = MovementHelpers.MoveAlongDirection(
                        _trackedWorldPosition, step.MovementDirection,
                        MathF.Min(step.MoveDistance, step.DistanceToTarget));
                    theObject.WorldPosition = new Vector3(
                        _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
                    var heading = MovementHelpers.GetHeadingFromMovementDirection(step.MovementDirection);
                    theObject.Rotation = new Vector3(heading.X, heading.Y, heading.Z);
                }
            }

        }

        private void SyncAuthoritativeTransform(I3dObject theObject)
        {
            // The minimap reads the original, including its rotated collision centre.
            // Copy values so later changes to the render copy cannot mutate that snapshot.
            foreach (var original in GameState.SurfaceState.AiObjects)
            {
                if (original.ObjectId == theObject.ObjectId && _trackedWorldPosition != null)
                {
                    if (ReferenceEquals(original, theObject)) return;
                    original.WorldPosition = new Vector3(
                        _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
                    original.ObjectOffsets = KamikazeDroneMovementHelpers.ToVector3(theObject.ObjectOffsets);
                    original.Rotation = KamikazeDroneMovementHelpers.ToVector3(theObject.Rotation);
                    return;
                }
            }
        }

        private void LogPosition(I3dObject theObject, DateTime now)
        {
            if (Logger.EnableFileLogging && (now - _lastPositionLogTime).TotalSeconds >= PositionLogIntervalSeconds)
            {
                _lastPositionLogTime = now;
                var position = theObject.WorldPosition;
                var shipPosition = GameState.ShipState?.ShipWorldPosition;
                // Re-evaluate after this frame's rotation and surface correction.
                // Both distance operands now describe enemy world-origin positions.
                var target = ShipTargetWorldPosition == null
                    ? null
                    : SurfacePositionSyncHelpers.GetShipRamTargetWorldPosition(theObject);
                string distance = target == null ? "unavailable" :
                    MovementHelpers.GetLength(MovementHelpers.GetVectorToTarget(
                        KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition),
                        target)).ToString("F1");
                Logger.Log(
                    $"Id={theObject.ObjectId} OnScreen={theObject.IsOnScreen} " +
                    $"WorldPosition=({position?.x:F1}, {position?.y:F1}, {position?.z:F1}) " +
                    $"ShipWorldPosition=({shipPosition?.x:F1}, {shipPosition?.y:F1}, {shipPosition?.z:F1}) " +
                    $"RamTargetWorldPosition=({target?.x:F1}, {target?.y:F1}, {target?.z:F1}) " +
                    $"DistanceToShip={distance} ImpactStatus=({theObject.ImpactStatus?.ObjectHealth:F1})",
                    "AttackShip");
            }

        }

        public void Dispose()
        {
            _audio = null;
            _explosionSound = null;
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audio != null || audioPlayer == null || soundRegistry == null) return;
            _audio = audioPlayer;
            _explosionSound = soundRegistry.Get("explosion_main");
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            if (++_framesSinceRelease < FramesBetweenReleases)
                return;

            var leftStart = GetCurrentFrameRotatedGuide(theObject, "AttackShipLeftEngineStartGuide");
            var leftGuide = GetCurrentFrameRotatedGuide(theObject, "AttackShipLeftEngineDirectionGuide");
            var rightStart = GetCurrentFrameRotatedGuide(theObject, "AttackShipRightEngineStartGuide");
            var rightGuide = GetCurrentFrameRotatedGuide(theObject, "AttackShipRightEngineDirectionGuide");

            bool leftReady = leftStart != null && leftGuide != null;
            bool rightReady = rightStart != null && rightGuide != null;
            if (!leftReady && !rightReady)
                return;

            _framesSinceRelease = 0;

            var worldPosition = new Vector3
            {
                x = theObject.WorldPosition?.x ?? 0f,
                y = theObject.WorldPosition?.y ?? 0f,
                z = theObject.WorldPosition?.z ?? 0f
            };

            if (leftReady)
                theObject.Particles?.ReleaseParticles(
                    leftGuide!, leftStart!, worldPosition, this, ParticleThrust, null);

            if (rightReady)
                theObject.Particles?.ReleaseParticles(
                    rightGuide!, rightStart!, worldPosition, this, ParticleThrust, null);
        }

        private ITriangleMeshWithColorAndTexture? GetCurrentFrameRotatedGuide(I3dObject theObject, string partName)
        {
            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part?.Triangles == null || part.Triangles.Count == 0)
                return null;

            var rotation = theObject.Rotation ?? new Vector3();
            var mesh = new List<ITriangleMeshWithColorAndTexture>
            {
                OmegaObjectHelpers.CopyTriangle(part.Triangles[0])
            };

            mesh = _rotate.RotateZMesh(mesh, rotation.z);
            mesh = _rotate.RotateYMesh(mesh, rotation.y);
            mesh = _rotate.RotateXMesh(mesh, rotation.x);

            return mesh[0];
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
            if (StartCoord != null) RearEngineStartCoordinates = StartCoord;
            if (GuideCoord != null) RearEngineGuideCoordinates = GuideCoord;
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
    }
}
