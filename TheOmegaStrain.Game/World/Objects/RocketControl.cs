using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Gameplay.Controls
{
    /// <summary>
    /// Movement and exhaust control for the Rocket.
    /// Flies straight initially, then turns around after a delay to strike its target ship.
    /// </summary>
    public sealed class RocketControls : IObjectMovement
    {
        private const string StartGuidePartName = "RocketParticlesStartGuide";
        private const string DirectionGuidePartName = "RocketParticlesDirectionGuide";

        private const int FramesBetweenReleases = 2;
        private const int ParticleThrust = 3;

        private const float RollSpeed = 2f;
        private const float PitchSpeed = 1.5f;
        private const float ForwardSpeed = 4f;

        // --- Target & Delay Configuration ---
        public I3dObject? TargetShip { get; set; }
        public int FramesUntilTurnaround { get; set; } = 120; // Time delay before turnaround (~2 sec at 60 FPS)
        public float HomingTurnSpeed { get; set; } = 8f;        // Steering rate toward target (deg/frame)
        public float HitRadius { get; set; } = 15f;             // Impact threshold distance

        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineStartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? RearEngineGuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; } = null!;
        public IPhysics Physics { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private float _accumulatedX;
        private float _accumulatedZ;
        private int _framesSinceRelease;
        private int _elapsedFrames;
        private readonly OmegaMeshRotation _rotate = new();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            if (theObject.ImpactStatus?.HasExploded == true)
                return theObject;

            _elapsedFrames++;
            var worldPos = theObject.WorldPosition as Vector3 ?? new Vector3();

            // Detonate on target proximity after turnaround sequence begins
            if (_elapsedFrames >= FramesUntilTurnaround && ShouldExplode(worldPos))
            {
                Explode(theObject);
                return theObject;
            }

            // Navigation: Steer back to AttackShip after timer expires
            if (_elapsedFrames >= FramesUntilTurnaround && TargetShip?.WorldPosition is Vector3 targetPos)
            {
                SteerTowardTarget(worldPos, targetPos);
            }
            else
            {
                _accumulatedZ += PitchSpeed;
            }

            _accumulatedX += RollSpeed;

            var rotation = theObject.Rotation as Vector3 ?? new Vector3();
            rotation.x += _accumulatedX;
            rotation.z = _accumulatedZ;
            theObject.Rotation = rotation;

            float pitchRad = rotation.z * (float)(Math.PI / 180.0);
            worldPos.x += (float)Math.Cos(pitchRad) * ForwardSpeed;
            worldPos.y += (float)Math.Sin(pitchRad) * ForwardSpeed;
            theObject.WorldPosition = worldPos;

            ReleaseParticles(theObject);
            if (theObject.Particles?.Particles.Count > 0)
                theObject.Particles.MoveParticles();

            return theObject;
        }

        private void SteerTowardTarget(Vector3 currentPos, Vector3 targetPos)
        {
            float dx = targetPos.x - currentPos.x;
            float dy = targetPos.y - currentPos.y;

            float targetAngleDeg = (float)(Math.Atan2(dy, dx) * (180.0 / Math.PI));

            float delta = (targetAngleDeg - _accumulatedZ) % 360f;
            if (delta > 180f) delta -= 360f;
            if (delta < -180f) delta += 360f;

            float step = Math.Clamp(delta, -HomingTurnSpeed, HomingTurnSpeed);
            _accumulatedZ += step;
        }

        private bool ShouldExplode(Vector3 worldPos)
        {
            if (TargetShip?.WorldPosition is Vector3 targetPos)
            {
                float dx = targetPos.x - worldPos.x;
                float dy = targetPos.y - worldPos.y;
                return ((dx * dx) + (dy * dy)) <= (HitRadius * HitRadius);
            }

            return false;
        }

        private void Explode(I3dObject theObject)
        {
            if (theObject.ImpactStatus != null)
            {
                theObject.ImpactStatus.HasExploded = true;
                theObject.ImpactStatus.HasCrashed = true;
            }

            // Flag the launching AttackShip for destruction on impact
            if (TargetShip?.ImpactStatus != null)
            {
                TargetShip.ImpactStatus.HasCrashed = true;
            }
        }

        public void Dispose() { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }

        public void ReleaseParticles(I3dObject theObject)
        {
            if (++_framesSinceRelease < FramesBetweenReleases)
                return;

            var start = GetCurrentFrameRotatedGuide(theObject, StartGuidePartName);
            var guide = GetCurrentFrameRotatedGuide(theObject, DirectionGuidePartName);
            if (start == null || guide == null)
                return;

            _framesSinceRelease = 0;

            var worldPosition = new Vector3
            {
                x = theObject.WorldPosition?.x ?? 0f,
                y = theObject.WorldPosition?.y ?? 0f,
                z = theObject.WorldPosition?.z ?? 0f
            };

            theObject.Particles?.ReleaseParticles(
                guide, start, worldPosition, this, ParticleThrust, null);
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

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
    }
}