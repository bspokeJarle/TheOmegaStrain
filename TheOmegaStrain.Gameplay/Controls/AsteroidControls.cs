using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    /// <summary>
    /// Moves a single asteroid across the screen in a straight diagonal streak,
    /// then respawns it off-screen with a random delay.
    /// </summary>
    public class AsteroidControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
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
        // Rotation spin
        private float _rotY;
        private float _spinSpeed;

        // Countdown until next visible pass.
        private float _waitSeconds;
        private bool _traveling;

        private readonly Random _rng;
        private readonly float _depth;

        private const float MinSpeed = 4.5f;
        private const float MaxSpeed = 9.0f;
        private const float MinWaitSeconds = 180f / GameState.GameplayBaselineFps;
        private const float MaxWaitSeconds = 500f / GameState.GameplayBaselineFps;
        private const float TrailEmissionIntervalSeconds = 2f / GameState.GameplayBaselineFps;
        private const int TrailThrust = 2;
        private const float TrailStartDistance = 14f;
        private const float TrailGuideDistance = 86f;

        // Optional forced direction (null = fully random)
        private bool? _forcedRight;
        private bool? _forcedDown;
        private ForcedScreenPath? _forcedScreenPath;
        private float _trailEmissionSeconds;

        public bool EmitTrailParticles { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;

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
            _rotY = (float)(_rng.NextDouble() * 360.0);
            _spinSpeed = 0.4f + (float)_rng.NextDouble() * 1.2f;
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

            // Spin
            float frameScale = GameState.FrameScale90;
            _rotY += _spinSpeed * frameScale;
            if (_rotY > 360f) _rotY -= 360f;
            if (theObject.Rotation != null)
                theObject.Rotation.y = _rotY;

            // Move (use persistent position so movement is not lost
            // when LiveGameLoop deep-copies the asteroid each frame).
            if (!_hasPosition && theObject.ObjectOffsets != null)
            {
                _curX = theObject.ObjectOffsets.x;
                _curY = theObject.ObjectOffsets.y;
                _hasPosition = true;
            }
            _curX += _vx * frameScale;
            _curY += _vy * frameScale;
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

            theObject.ObjectOffsets.x = ox;
            theObject.ObjectOffsets.y = oy;
            theObject.ObjectOffsets.z = _depth;
            _curX = ox;
            _curY = oy;
            _hasPosition = true;
            theObject.IsActive = true;
            _traveling = true;
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
                float length = MathF.Sqrt((_vx * _vx) + (_vy * _vy));
                if (length > 0.001f && theObject.ObjectOffsets != null)
                {
                    float dirX = _vx / length;
                    float dirY = _vy / length;
                    // Emit particles BEHIND the asteroid that drift further
                    // backwards. ReleaseParticles computes velocity as
                    // (startPos - guidePos) / life, so to make particles fly
                    // opposite to the asteroid's travel direction we put
                    // 'start' just behind the asteroid and 'guide' AHEAD of
                    // it. That makes (start - guide) point opposite to
                    // travel direction, producing a true rear-engine trail.
                    var start = CreatePointTriangle(-dirX * TrailStartDistance, -dirY * TrailStartDistance, 0f);
                    var guide = CreatePointTriangle( dirX * TrailGuideDistance,  dirY * TrailGuideDistance, -8f);
                    theObject.Particles.ReleaseParticles(guide, start, theObject.ObjectOffsets, this, TrailThrust, false);
                }

                _trailEmissionSeconds = 0f;
            }

            theObject.Particles.MoveParticles();
        }

        private static TriangleMeshWithColor CreatePointTriangle(float x, float y, float z)
        {
            return new TriangleMeshWithColor
            {
                Color = "FFFFFF",
                vert1 = new Vector3 { x = x - 0.5f, y = y, z = z },
                vert2 = new Vector3 { x = x + 0.5f, y = y, z = z },
                vert3 = new Vector3 { x = x, y = y + 0.5f, z = z },
                noHidden = true
            };
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor s, ITriangleMeshWithColor g) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor s, ITriangleMeshWithColor g) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor s, ITriangleMeshWithColor g) { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }

        public void Dispose()
        {
            _traveling = false;
        }

        private float RandomWaitSeconds()
        {
            return MinWaitSeconds + (float)_rng.NextDouble() * (MaxWaitSeconds - MinWaitSeconds);
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
