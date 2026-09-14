using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Gameplay.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.CommonSetup;

namespace TheOmegaStrain.Gameplay.Controls
{
    public sealed class AttackShipControls : IObjectMovement
    {
        // Engines emit a steady stream, alternating frames to keep the particle count sane.
        private const int FramesBetweenReleases = 2;
        private const int ParticleThrust = 3;
        private const float SecondsPerScreenCrossing = 3f;
        private const float MaximumMovementDeltaSeconds = 1f;
        private const double PositionLogIntervalSeconds = 1;
        private const int overFlowMarginY = 150;
        private const int overFlowMarginZ = 250;

        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineStartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineGuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();
        public Vector3? ShipTargetWorldPosition { get; private set; }
        public Vector3? DirectionToShip { get; private set; }
        // Controllers survive the engine's per-frame deep copies of scene objects.
        // Keep the authoritative position and captured scene height here.
        private float? _initialOffsetY;
        public DateTime LastMovementDateTime;
        private DateTime? _lastMovementTime;
        private DateTime _lastPositionLogTime = DateTime.MinValue;
        private int _framesSinceRelease;
        private readonly OmegaMeshRotation _rotate = new();
        private Vector3? _trackedWorldPosition;
        private bool _isExploding = false;
        private DateTime _explosionDeltaTime;

        private Vector3 _explosionWorldPosition;
        private Vector3 _explosionObjectOffsets;

        private Vector3? _storedWorldPosition;
        private bool _storedWorldPositionInitialized;


        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            var now = DateTime.Now;
            float deltaSeconds = GetMovementDeltaSeconds(now);

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
                SyncAuthoritativePosition(theObject);
                LastMovementDateTime = DateTime.Now;
                return theObject;
            }
            if (theObject.ImpactStatus?.HasCrashed == true && !_isExploding)
            {
                HandleCrash(theObject);
            }

            RestoreMovementState(theObject);
            PursueShip(theObject, deltaSeconds);
            SyncAuthoritativePosition(theObject);

            // Apply the view correction after navigation and before emitting exhaust.
            SurfacePositionSyncHelpers.AddSurfacePitchHeightCorrectionY(
                theObject, WorldViewSetup.SurfacePitchDegrees);
            LogPosition(theObject, now);

            ReleaseParticles(theObject);
            if (theObject.Particles?.Particles.Count > 0)
                theObject.Particles.MoveParticles();

            return theObject;
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
            // Use the Drone's converted Ship crash centre, including its fallback
            // to ShipWorldPosition. Raw ShipState coordinates use a different origin.
            ShipTargetWorldPosition = KamikazeDroneMovementHelpers.GetShipCrashCenterWorldPosition();
            DirectionToShip = ShipTargetWorldPosition == null
                ? null
                : MovementHelpers.GetVectorToTarget(
                    KamikazeDroneMovementHelpers.GetDroneCrashCenterWorldPosition(theObject),
                    ShipTargetWorldPosition);

            if (ShipTargetWorldPosition != null && _trackedWorldPosition != null)
            {
                var centre = KamikazeDroneMovementHelpers.GetDroneCrashCenterWorldPosition(theObject);
                var velocity = MovementHelpers.GetVelocityTowardsTarget(centre, ShipTargetWorldPosition,
                    MovementHelpers.GetScreenCrossingSpeed(SecondsPerScreenCrossing));
                var step = MovementHelpers.GetPursuitStep(centre, ShipTargetWorldPosition, velocity, deltaSeconds);
                if (step.HasMovement)
                {
                    // Stop at the target instead of stepping past it on a long frame.
                    _trackedWorldPosition = MovementHelpers.MoveAlongDirection(
                        _trackedWorldPosition, step.MovementDirection,
                        MathF.Min(step.MoveDistance, MovementHelpers.GetLength(DirectionToShip!)));
                    theObject.WorldPosition = new Vector3(
                        _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
                    var heading = MovementHelpers.GetHeadingFromMovementDirection(step.MovementDirection);
                    theObject.Rotation = new Vector3(heading.X, heading.Y, heading.Z);
                }
            }

        }

        private void SyncAuthoritativePosition(I3dObject theObject)
        {
            // Keep the original used by visibility checks in sync with the moving copy.
            foreach (var original in GameState.SurfaceState.AiObjects)
                if (original.ObjectId == theObject.ObjectId && _trackedWorldPosition != null)
                    original.WorldPosition = new Vector3(
                        _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
        }

        private void LogPosition(I3dObject theObject, DateTime now)
        {
            if (Logger.EnableFileLogging && (now - _lastPositionLogTime).TotalSeconds >= PositionLogIntervalSeconds)
            {
                _lastPositionLogTime = now;
                var position = theObject.WorldPosition;
                var shipPosition = GameState.ShipState?.ShipWorldPosition;
                var target = ShipTargetWorldPosition;
                string distance = target == null ? "unavailable" :
                    MovementHelpers.GetLength(MovementHelpers.GetVectorToTarget(
                        KamikazeDroneMovementHelpers.GetDroneCrashCenterWorldPosition(theObject),
                        target)).ToString("F1");
                Logger.Log(
                    $"Id={theObject.ObjectId} OnScreen={theObject.IsOnScreen} " +
                    $"WorldPosition=({position?.x:F1}, {position?.y:F1}, {position?.z:F1}) " +
                    $"ShipWorldPosition=({shipPosition?.x:F1}, {shipPosition?.y:F1}, {shipPosition?.z:F1}) " +
                    $"ShipTarget=({target?.x:F1}, {target?.y:F1}, {target?.z:F1}) " +
                    $"DistanceToShip={distance}",
                    "AttackShip");
            }

        }

        public void Dispose() { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }

        private void HandleCrash(I3dObject theObject)
        {
            if (theObject.ImpactStatus == null)
            {
                return;
            }

            int currentHealth = theObject.ImpactStatus.ObjectHealth ?? EnemySetup.AttackShipHealth;
            int damage = theObject.ImpactStatus.ObjectName switch
            {
                "Ship" => EnemySetup.AttackShipHealth,
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


            _isExploding = true;
            _explosionDeltaTime = DateTime.Now;
            _explosionWorldPosition = theObject.WorldPosition as Vector3 ?? KamikazeDroneMovementHelpers.ToVector3(theObject.WorldPosition);
            _explosionObjectOffsets = theObject.ObjectOffsets as Vector3 ?? KamikazeDroneMovementHelpers.ToVector3(theObject.ObjectOffsets);

            ExplosionParticleHelpers.ReleaseExplosionParticles(theObject, this);
            Physics.ExplodeObject(theObject, 200f);
            theObject.CrashBoxes = new List<List<IVector3>>();
            theObject.ImpactStatus.HasCrashed = false;
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
