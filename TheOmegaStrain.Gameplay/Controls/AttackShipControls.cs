using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class AttackShipControls : IObjectMovement
    {
        private readonly OmegaMeshRotation _rotate = new();

        internal static Vector3 ToVector3(IVector3? v)
        {
            if (v is null)
            {
                return new Vector3();
            }

            return new Vector3
            {
                x = v.x,
                y = v.y,
                z = v.z
            };
        }

        private Vector3 _accumulatedRotation = new Vector3();
        private float _accumulatedX;
        private float _accumulatedY;


        // How fast the ship swings toward its heading; lower = lazier turns.
        private const float TurnSpeedDegreesPerSecond = 50f;
        // Fraction of speed retained when facing 90+ degrees away from the target,
        // so the ship slows through a hard turn but never fully stalls.
        private const float MinTurnThrottle = 0.15f;
        private const float MaximumMovementDeltaSeconds = 0.1f;
        private const float YRotationSpeed = 1f;
        private const int FramesBetweenReleases = 2;
        private const int ParticleThrust = 10;

        private const int IdealFiringDistance = 500;
        // Distance outside the firing ring over which the ship eases from full speed
        // down to a stop, so it coasts in and holds station instead of overshooting.
        private const float ApproachSlowdownBand = 400f;


        public Vector3? ShipTargetWorldPosition { get; private set; }
        private float? _initialOffsetY;
        private DateTime? _lastMovementTime;
        private int _framesSinceRelease;
        private Vector3? _trackedWorldPosition;
        private Vector3? _trackedRotation;
        public Vector3? DirectionToShip { get; private set; }
        private const float SecondsPerScreenCrossing = 2f;
        private const float BaseYRotation = 0f;
        private static float BaseXRotation => WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        

        private float GetMovementDeltaSeconds(DateTime now)
        {
            // Off-screen AI updates are less frequent; measure elapsed time locally.
            float dt = _lastMovementTime.HasValue
                ? Math.Clamp((float)(now - _lastMovementTime.Value).TotalSeconds, 0f, MaximumMovementDeltaSeconds)
                : 0f;
            _lastMovementTime = now;
            return dt;
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            var rotation = theObject.Rotation as Vector3 ?? new Vector3();
            var now = DateTime.Now;
            var deltaSeconds = GetMovementDeltaSeconds(now);
            RestoreMovementState(theObject);
            PursueShip(theObject, deltaSeconds);
            SyncAuthoritativePosition(theObject);

            // Apply the view correction after navigation and before emitting exhaust.
            SurfacePositionSyncHelpers.AddSurfacePitchHeightCorrectionY(
                theObject, WorldViewSetup.SurfacePitchDegrees);


            ReleaseParticles(theObject);
            if(theObject.Particles?.Particles.Count > 0)
            {
                theObject.Particles.MoveParticles();
            }

            return theObject;
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

            // Persist rotation across frames so the eased turn continues where it left off.
            if (_trackedRotation == null && theObject.Rotation is Vector3 startRotation)
                _trackedRotation = new Vector3(startRotation.x, startRotation.y, startRotation.z);
            if (_trackedRotation != null)
                theObject.Rotation = new Vector3(
                    _trackedRotation.x, _trackedRotation.y, _trackedRotation.z);
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
                    // Ease the rotation toward the target heading first, then fly along
                    // whichever way the ship is now actually pointing so it curves in.
                    var heading = MovementHelpers.GetHeadingFromMovementDirection(step.MovementDirection);
                    var current = theObject.Rotation as Vector3 ?? new Vector3();
                    var eased = MovementHelpers.MoveRotationTowards(
                        current.x, current.y, current.z,
                        heading.X, heading.Y, heading.Z,
                        TurnSpeedDegreesPerSecond,
                        deltaSeconds);
                    _trackedRotation = new Vector3(eased.X, eased.Y, eased.Z);
                    theObject.Rotation = new Vector3(eased.X, eased.Y, eased.Z);

                    // Move forward along the current heading. Slow down mid-turn: the more
                    // the heading is off from the target, the shorter the step.
                    var forward = MovementHelpers.GetForwardDirectionFromHeading(_trackedRotation);
                    var turnThrottle = MathF.Max(
                        MovementHelpers.Dot(forward, step.MovementDirection), MinTurnThrottle);

                    // Hold station at the firing distance: ease speed to zero as the ship
                    // closes the gap to the ideal ring, and never step inside it.
                    var distanceToRing = MovementHelpers.GetLength(DirectionToShip!) - IdealFiringDistance;
                    var distanceThrottle = Math.Clamp(distanceToRing / ApproachSlowdownBand, 0f, 1f);
                    var maxStep = MathF.Max(distanceToRing, 0f);

                    _trackedWorldPosition = MovementHelpers.MoveAlongDirection(
                        _trackedWorldPosition, forward,
                        MathF.Min(step.MoveDistance * turnThrottle * distanceThrottle, maxStep));
                    theObject.WorldPosition = new Vector3(
                        _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
                }
            }

        }

        private void SyncAuthoritativePosition(I3dObject theObject)
        {
            // Keep the original used by visibility checks in sync with the moving copy.
            foreach (var original in GameState.SurfaceState.AiObjects)
                if (original.ObjectId == theObject.ObjectId)
                {
                    if (_trackedWorldPosition != null)
                        original.WorldPosition = new Vector3(
                            _trackedWorldPosition.x, _trackedWorldPosition.y, _trackedWorldPosition.z);
                    if (_trackedRotation != null)
                        original.Rotation = new Vector3(
                            _trackedRotation.x, _trackedRotation.y, _trackedRotation.z);
                }
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord)
        {
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
        }

        public void Dispose()
        {
            StartCoordinates = null;
            GuideCoordinates = null;
        }
    }
}
