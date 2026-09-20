using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Gameplay.Controls
{
    /// <summary>
    /// Moves a single asteroid across the screen in a straight diagonal streak,
    /// then respawns it off-screen with a random delay.
    /// </summary>
    public class AsteroidControls : IObjectMovement
    {
        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public I3dObject? ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        // Travel direction per frame
        private float _vx;
        private float _vy;
        // Persistent travel position across frames. Required because
        // LiveGameLoop deep-copies the asteroid every frame and runs
        // MoveObject on the copy, so mutating theObject.ObjectOffsets
        // would never persist back to the original WorldInhabitants
        // entry. Storing the live position on the controller (which is
        // shared between original and copy via Movement reference)
        // ensures the asteroid actually travels across the screen.
        private float _curX;
        private float _curY;
        private bool _hasPosition;
        // LiveGameLoop deep-copies the object every frame. Keep the heading on the
        // shared controller and reapply it to each copy, like the Drone controllers.
        private float _travelRotationZ = 90f;
        private float _bodyRollDegrees;
        private readonly float _bodyRollDegreesPerSecond;
        // Countdown until next visible pass.
        private float _waitSeconds;
        private bool _traveling;

        private readonly Random _rng;
        private readonly float _depth;
        private float _minWaitSeconds = MinWaitSeconds;
        private float _maxWaitSeconds = MaxWaitSeconds;
        private bool _topDownCrossingsOnly;

        private const float MinSpeed = 4.5f;
        private const float MaxSpeed = 9.0f;
        private const float MinWaitSeconds = 180f / GameState.GameplayBaselineFps;
        private const float MaxWaitSeconds = 500f / GameState.GameplayBaselineFps;
        private const float TrailEmissionIntervalSeconds = 2f / GameState.GameplayBaselineFps;
        private const int TrailThrust = 2;
        private const string ParticleStartGuidePartName = "AsteroidParticlesStartGuide";
        private const string ParticleDirectionGuidePartName = "AsteroidParticlesDirectionGuide";

        // Optional forced direction (null = fully random)
        private bool? _forcedRight;
        private bool? _forcedDown;
        private ForcedScreenPath? _forcedScreenPath;
        private float _trailEmissionSeconds;
        private readonly OmegaMeshRotation _meshRotation = new();

        public bool EmitTrailParticles { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// Reuses the asteroid as an occasional meteor entering from above.
        /// Each pass chooses a new side and downward angle.
        /// </summary>
        public void ConfigureTopDownCrossings(float minWaitSeconds, float maxWaitSeconds)
        {
            _minWaitSeconds = Math.Max(0f, minWaitSeconds);
            _maxWaitSeconds = Math.Max(_minWaitSeconds, maxWaitSeconds);
            _topDownCrossingsOnly = true;
            _waitSeconds = RandomWaitSeconds();
        }

        /// <summary>
        /// Lock the crossing direction for this asteroid so it always enters from a consistent edge.
        /// Call once after construction, before the first frame.
        /// </summary>
        public void ForceDirection(bool directionRight, bool directionDown)
        {
            _forcedRight = directionRight;
            _forcedDown  = directionDown;
        }

        /// <summary>
        /// Lock the crossing path to factors of the asteroid spawn half-width/half-height.
        /// Values near -1/1 place the asteroid at the screen edge while still keeping it visible.
        /// </summary>
        public void ForceScreenPath(float startXFactor, float startYFactor, float targetXFactor, float targetYFactor)
        {
            _forcedScreenPath = new ForcedScreenPath(startXFactor, startYFactor, targetXFactor, targetYFactor);
        }

        /// <param name="startImmediately">When true the asteroid begins crossing the screen right away instead of waiting.</param>
        public AsteroidControls(Random rng, float depth, bool startImmediately = false)
        {
            _rng = rng;
            _depth = depth;
            _bodyRollDegrees = (float)(_rng.NextDouble() * 360.0);
            float rollDirection = _rng.Next(2) == 0 ? -1f : 1f;
            _bodyRollDegreesPerSecond = rollDirection * (45f + (float)_rng.NextDouble() * 75f);
            _waitSeconds = startImmediately ? 0f : RandomWaitSeconds();
            _traveling = false;
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            if (!_traveling)
            {
                theObject.Particles?.MoveParticles();
                _waitSeconds -= GameState.ClampedDeltaTime;
                if (_waitSeconds <= 0f)
                    Spawn(theObject);
                return theObject;
            }

            float frameScale = GameState.FrameScale90;
            ApplyTravelRotation(theObject);
            ApplyBodyRoll(theObject);

            // Move (use persistent position so movement is not lost
            // when LiveGameLoop deep-copies the asteroid each frame).
            if (!_hasPosition && theObject.ObjectOffsets != null)
            {
                _curX = theObject.ObjectOffsets.x;
                _curY = theObject.ObjectOffsets.y;
                _hasPosition = true;
            }
            float frameMoveX = _vx * frameScale;
            float frameMoveY = _vy * frameScale;
            KeepExistingTrailIndependentOfEmitter(theObject, frameMoveX, frameMoveY);
            _curX += frameMoveX;
            _curY += frameMoveY;
            if (theObject.ObjectOffsets != null)
            {
                theObject.ObjectOffsets.x = _curX;
                theObject.ObjectOffsets.y = _curY;
            }
            EmitTrail(theObject);

            // Off-screen? wait again
            float hw = ScreenSetup.screenSizeX * 0.7f;
            float hh = ScreenSetup.screenSizeY * 0.7f;
            if (_curX < -hw || _curX > hw || _curY < -hh || _curY > hh)
            {
                _traveling = false;
                _hasPosition = false;
                _waitSeconds = RandomWaitSeconds();
                // Repeating background meteors must remain in the live update set while
                // their next-pass timer counts down. One-shot cinematic asteroids retain
                // the established inactive state after leaving the screen.
                if (!_topDownCrossingsOnly)
                    theObject.IsActive = false;
            }

            return theObject;
        }

        private void Spawn(I3dObject theObject)
        {
            float hw = ScreenSetup.screenSizeX * 0.55f;
            float hh = ScreenSetup.screenSizeY * 0.55f;

            float ox, oy;

            if (_forcedScreenPath != null)
            {
                ox = _forcedScreenPath.StartXFactor * hw;
                oy = _forcedScreenPath.StartYFactor * hh;
            }
            else if (_topDownCrossingsOnly)
            {
                float side = _rng.Next(2) == 0 ? -1f : 1f;
                ox = side * hw * (0.45f + (float)_rng.NextDouble() * 0.35f);
                oy = -hh;
            }
            else if (_forcedRight.HasValue && _forcedDown.HasValue)
            {
                // Enter from the top edge, starting x-side determined by direction
                ox = _forcedRight.Value
                    ? -(hw * 0.4f + (float)_rng.NextDouble() * hw * 0.3f)   // enter from left side of top
                    : (hw * 0.4f + (float)_rng.NextDouble() * hw * 0.3f);   // enter from right side of top
                oy = -hh;
            }
            else
            {
                int edge = _rng.Next(4);
                switch (edge)
                {
                    case 0: ox = -hw; oy = (float)(_rng.NextDouble() * hh * 2 - hh); break;
                    case 1: ox =  hw; oy = (float)(_rng.NextDouble() * hh * 2 - hh); break;
                    case 2: ox = (float)(_rng.NextDouble() * hw * 2 - hw); oy = -hh; break;
                    default:ox = (float)(_rng.NextDouble() * hw * 2 - hw); oy =  hh; break;
                }
            }

            // Target: opposite side, respecting forced direction if set
            float targetX, targetY;
            if (_forcedScreenPath != null)
            {
                targetX = _forcedScreenPath.TargetXFactor * hw;
                targetY = _forcedScreenPath.TargetYFactor * hh;
            }
            else if (_topDownCrossingsOnly)
            {
                float oppositeSide = -MathF.Sign(ox);
                // Aim beyond the opposite side while staying in the upper half. The
                // meteor therefore crosses the distant sky and never appears to strike
                // the visible Surface.
                targetX = oppositeSide * hw * (1.35f + (float)_rng.NextDouble() * 0.25f);
                targetY = hh * (-0.10f + (float)_rng.NextDouble() * 0.35f);
            }
            else if (_forcedRight.HasValue && _forcedDown.HasValue)
            {
                float xSign = _forcedRight.Value ? 1f : -1f;
                targetX = xSign * (hw * 0.3f + (float)_rng.NextDouble() * hw * 0.4f);
                targetY = hh * (0.5f + (float)_rng.NextDouble() * 0.5f);
            }
            else
            {
                targetX = (float)(_rng.NextDouble() * hw * 1.2 - hw * 0.6) * -MathF.Sign(ox + 0.001f);
                targetY = (float)(_rng.NextDouble() * hh * 1.2 - hh * 0.6) * -MathF.Sign(oy + 0.001f);
            }

            float dx = targetX - ox;
            float dy = targetY - oy;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            float speed = MinSpeed + (float)_rng.NextDouble() * (MaxSpeed - MinSpeed);
            speed *= Math.Max(0.1f, SpeedMultiplier);
            _vx = dx / len * speed;
            _vy = dy / len * speed;

            // The pointed nose lies along local -Y. Store the heading on the controller
            // because the object instance itself is replaced by a deep copy next frame.
            _travelRotationZ = MathF.Atan2(_vx, -_vy) * 180f / MathF.PI;
            ApplyTravelRotation(theObject);

            theObject.ObjectOffsets.x = ox;
            theObject.ObjectOffsets.y = oy;
            theObject.ObjectOffsets.z = _depth;
            _curX = ox;
            _curY = oy;
            _hasPosition = true;
            theObject.IsActive = true;
            _traveling = true;
        }

        private void ApplyTravelRotation(I3dObject theObject)
        {
            if (theObject.Rotation == null)
                return;

            theObject.Rotation.y = 0f;
            theObject.Rotation.z = _travelRotationZ;
        }

        private void ApplyBodyRoll(I3dObject theObject)
        {
            _bodyRollDegrees += _bodyRollDegreesPerSecond * GameState.ClampedDeltaTime;
            if (_bodyRollDegrees >= 360f) _bodyRollDegrees -= 360f;
            if (_bodyRollDegrees < 0f) _bodyRollDegrees += 360f;

            var body = theObject.ObjectParts?.Find(part => part.PartName == "AsteroidBody");
            if (body?.Triangles == null || body.Triangles.Count == 0)
                return;

            // Roll only the visible body around its local length axis. The hidden
            // particle guides deliberately remain untouched and keep pointing aft.
            body.Triangles = _meshRotation.RotateYMesh(body.Triangles, _bodyRollDegrees);
        }

        private static void KeepExistingTrailIndependentOfEmitter(
            I3dObject theObject,
            float emitterMoveX,
            float emitterMoveY)
        {
            if (theObject.Particles?.Particles == null)
                return;

            // ParticleManager adds the emitter's current ObjectOffsets when rendering.
            // Cancel this frame's emitter movement for particles that already exist so
            // the exhaust is left behind instead of being carried forward by the meteor.
            foreach (var particle in theObject.Particles.Particles)
            {
                if (particle.Position == null)
                    continue;

                particle.Position.x -= emitterMoveX;
                particle.Position.y -= emitterMoveY;
            }
        }

        private void EmitTrail(I3dObject theObject)
        {
            if (!EmitTrailParticles || theObject.Particles == null)
            {
                theObject.Particles?.MoveParticles();
                return;
            }

            _trailEmissionSeconds += GameState.ClampedDeltaTime;
            if (_trailEmissionSeconds >= TrailEmissionIntervalSeconds)
            {
                var start = GetCurrentFrameRotatedGuide(theObject, ParticleStartGuidePartName);
                var guide = GetCurrentFrameRotatedGuide(theObject, ParticleDirectionGuidePartName);
                if (start != null && guide != null)
                {
                    // ParticleManager already adds the emitter's ObjectOffsets. Use the
                    // world anchor here, as Drone and AttackShip do, to avoid applying
                    // the meteor's large screen/depth offset twice.
                    var worldPosition = theObject.WorldPosition ?? new Vector3();
                    theObject.Particles.ReleaseParticles(guide, start, worldPosition, this, TrailThrust, false);
                }

                _trailEmissionSeconds = 0f;
            }

            theObject.Particles.MoveParticles();
        }

        private ITriangleMeshWithColorAndTexture? GetCurrentFrameRotatedGuide(I3dObject theObject, string partName)
        {
            var part = theObject.ObjectParts?.Find(p => p.PartName == partName);
            if (part?.Triangles == null || part.Triangles.Count == 0)
                return null;

            var rotation = theObject.Rotation ?? new Vector3();
            var mesh = new List<ITriangleMeshWithColorAndTexture>
            {
                OmegaObjectHelpers.CopyTriangle(part.Triangles[0])
            };

            mesh = _meshRotation.RotateZMesh(mesh, rotation.z);
            mesh = _meshRotation.RotateYMesh(mesh, rotation.y);
            mesh = _meshRotation.RotateXMesh(mesh, rotation.x);
            return mesh[0];
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture s, ITriangleMeshWithColorAndTexture g)
        {
            if (s != null) StartCoordinates = s;
            if (g != null) GuideCoordinates = g;
        }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture s, ITriangleMeshWithColorAndTexture g) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture s, ITriangleMeshWithColorAndTexture g) { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }

        public void Dispose()
        {
            _traveling = false;
        }

        private float RandomWaitSeconds()
        {
            return _minWaitSeconds + (float)_rng.NextDouble() * (_maxWaitSeconds - _minWaitSeconds);
        }

        private sealed class ForcedScreenPath
        {
            public ForcedScreenPath(float startXFactor, float startYFactor, float targetXFactor, float targetYFactor)
            {
                StartXFactor = startXFactor;
                StartYFactor = startYFactor;
                TargetXFactor = targetXFactor;
                TargetYFactor = targetYFactor;
            }

            public float StartXFactor { get; }
            public float StartYFactor { get; }
            public float TargetXFactor { get; }
            public float TargetYFactor { get; }
        }
    }
}
