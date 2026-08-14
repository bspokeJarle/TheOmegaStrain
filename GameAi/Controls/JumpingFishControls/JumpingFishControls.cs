using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Helpers;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.JumpingFishControls
{
    public enum JumpStyle
    {
        Standard,
        LowSkimmer,
        HighArc
    }

    /// <summary>
    /// Per-spawn timing variation: makes individual fish or seals run at slightly
    /// different tempos, start at different points in the jump cycle, and rest in
    /// the water for a random amount of time between jumps.
    /// </summary>
    public readonly record struct JumpSpawnTiming(float SpeedMultiplier, float WaterDwellSeconds, float PhaseOffsetSeconds);

    public static class JumpStyleVariants
    {
        public static readonly IReadOnlyList<JumpStyle> All = new[]
        {
            JumpStyle.Standard,
            JumpStyle.LowSkimmer,
            JumpStyle.HighArc
        };

        private const float MinSpeedMultiplier = 0.85f;
        private const float MaxSpeedMultiplier = 1.20f;
        private const float MinWaterDwellSeconds = 0.4f;
        private const float MaxWaterDwellSeconds = 3.0f;

        public static JumpStyle PickRandom(Random random)
        {
            ArgumentNullException.ThrowIfNull(random);
            return All[random.Next(All.Count)];
        }

        /// <summary>
        /// Returns an alternating initial jump direction (-1 or +1) so that two consecutive
        /// fish or seals spawned next to each other start at opposite ends of their area
        /// instead of overlapping. The starting sign is randomized per spawn run so the
        /// pattern is not visually identical across scenes.
        /// </summary>
        public static int PickAlternatingDirection(Random random, int spawnIndex)
        {
            ArgumentNullException.ThrowIfNull(random);
            int startSign = random.Next(2) == 0 ? -1 : 1;
            return (spawnIndex % 2 == 0) ? startSign : -startSign;
        }

        /// <summary>
        /// Randomizes per-spawn tempo and water-dwell time so a group of fish or seals
        /// does not move in lockstep. PhaseOffsetSeconds further desynchronizes
        /// objects spawned together by starting them at different points in the cycle.
        /// </summary>
        public static JumpSpawnTiming PickSpawnTiming(Random random)
        {
            ArgumentNullException.ThrowIfNull(random);
            float speedMultiplier = Lerp(MinSpeedMultiplier, MaxSpeedMultiplier, (float)random.NextDouble());
            float waterDwellSeconds = Lerp(MinWaterDwellSeconds, MaxWaterDwellSeconds, (float)random.NextDouble());
            float phaseOffsetSeconds = (float)random.NextDouble() * (waterDwellSeconds + 2.5f);
            return new JumpSpawnTiming(speedMultiplier, waterDwellSeconds, phaseOffsetSeconds);
        }

        private static float Lerp(float min, float max, float t) => min + ((max - min) * t);
    }

    public class JumpingFishControls : IObjectMovement
    {
        private const float BaseXRotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private const float BaseYRotation = 0f;
        private const int InitialJumpDirection = -1;
        private const float StartZRotation = -90f;
        private const float DefaultJumpHorizontalSpan = 260f;
        private const float DefaultJumpRotationDegrees = 180f;
        private const float DefaultJumpDurationSeconds = 2.0f;
        private const float DefaultJumpHeight = 170f;
        private const float DefaultJumpDepthPulse = 18f;
        private const float DefaultTwistAmplitude = 24f;
        private const float DefaultApexXRotationLift = 8f;
        private const float LandingSplashPhase = 0.94f;
        private const float SubmergedDwellDepth = 100f;
        private const int SplashParticleThrust = 3;
        private const float SplashUpwardVelocityBoost = 4.5f;
        private const float SplashSurfaceLift = -12f;
        private const int TailFrameCount = 6;
        private const float TailFramesPerSecond = 14f;
        private const float FinRadiansPerSecond = 11.5f;
        private const float PectoralFinAmplitude = 26f;

        private const float LeftFinPivotY = -7.2f;
        private const float RightFinPivotY = 7.2f;
        private const float FinPivotZ = -1.6f;

        private readonly OmegaMeshRotation _rotate = new();
        private readonly Dictionary<string, List<ITriangleMeshWithColor>> _baseTrianglesByPart = new();
        private readonly float _jumpHorizontalSpan;
        private readonly bool _hasPathBounds;
        private readonly float _minPathOffsetX;
        private readonly float _maxPathOffsetX;
        private readonly int _initialJumpDirection;
        private readonly float _jumpRotationDegrees;
        private readonly float _jumpDurationSeconds;
        private readonly float _jumpHeight;
        private readonly float _jumpDepthPulse;
        private readonly float _twistAmplitude;
        private readonly float _apexXRotationLift;
        private readonly float _horizontalSpanMultiplier;
        private readonly float _waterDwellSeconds;
        private readonly float _initialPhaseOffsetSeconds;

        private DateTime _lastFrameTime = DateTime.MinValue;
        private float _jumpTimeSeconds;
        private float _baseOffsetX;
        private float _baseOffsetY;
        private float _baseOffsetZ;
        private bool _baseInitialized;
        private bool _firstPoseApplied;
        private bool _takeoffSplashReleased;
        private bool _landingSplashReleased;
        private bool _particleAnchorInitialized;
        private Vector3 _particleAnchorOffsets = new();
        private int _jumpDirection = InitialJumpDirection;

        private bool _audioConfigured;
        private IAudioPlayer? _audio;
        private SoundDefinition? _splashSound;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public JumpingFishControls()
            : this(DefaultJumpHorizontalSpan, JumpStyle.Standard)
        {
        }

        public JumpingFishControls(JumpStyle style)
            : this(DefaultJumpHorizontalSpan, style)
        {
        }

        public JumpingFishControls(float jumpHorizontalSpan)
            : this(jumpHorizontalSpan, JumpStyle.Standard)
        {
        }

        public JumpingFishControls(float jumpHorizontalSpan, JumpStyle style)
            : this(jumpHorizontalSpan, 0f, 0f, InitialJumpDirection, hasPathBounds: false, style, timing: null)
        {
        }

        public JumpingFishControls(float jumpHorizontalSpan, float minPathOffsetX, float maxPathOffsetX, int initialJumpDirection = InitialJumpDirection, JumpStyle style = JumpStyle.Standard)
            : this(jumpHorizontalSpan, minPathOffsetX, maxPathOffsetX, initialJumpDirection, hasPathBounds: true, style, timing: null)
        {
        }

        public JumpingFishControls(float jumpHorizontalSpan, float minPathOffsetX, float maxPathOffsetX, int initialJumpDirection, JumpStyle style, JumpSpawnTiming timing)
            : this(jumpHorizontalSpan, minPathOffsetX, maxPathOffsetX, initialJumpDirection, hasPathBounds: true, style, timing)
        {
        }

        private JumpingFishControls(float jumpHorizontalSpan, float minPathOffsetX, float maxPathOffsetX, int initialJumpDirection, bool hasPathBounds, JumpStyle style, JumpSpawnTiming? timing)
        {
            (_jumpRotationDegrees,
             _jumpDurationSeconds,
             _jumpHeight,
             _jumpDepthPulse,
             _twistAmplitude,
             _apexXRotationLift,
             _horizontalSpanMultiplier) = GetStyleValues(style);

            float speedMultiplier = timing.HasValue ? Math.Max(0.1f, timing.Value.SpeedMultiplier) : 1.0f;
            _jumpDurationSeconds = _jumpDurationSeconds / speedMultiplier;
            _waterDwellSeconds = timing.HasValue ? Math.Max(0f, timing.Value.WaterDwellSeconds) : 0f;
            _initialPhaseOffsetSeconds = timing.HasValue ? Math.Max(0f, timing.Value.PhaseOffsetSeconds) : 0f;

            _jumpHorizontalSpan = Math.Max(75f, jumpHorizontalSpan) * _horizontalSpanMultiplier;
            _minPathOffsetX = Math.Min(minPathOffsetX, maxPathOffsetX);
            _maxPathOffsetX = Math.Max(minPathOffsetX, maxPathOffsetX);
            _initialJumpDirection = initialJumpDirection < 0 ? -1 : 1;
            _jumpDirection = _initialJumpDirection;
            _hasPathBounds = hasPathBounds && (_maxPathOffsetX - _minPathOffsetX) > _jumpHorizontalSpan;
        }

        private static (float jumpRotationDegrees,
                        float jumpDurationSeconds,
                        float jumpHeight,
                        float jumpDepthPulse,
                        float twistAmplitude,
                        float apexXRotationLift,
                        float horizontalSpanMultiplier) GetStyleValues(JumpStyle style) => style switch
        {
            JumpStyle.LowSkimmer => (
                jumpRotationDegrees: 120f,
                jumpDurationSeconds: 2.5f,
                jumpHeight: 95f,
                jumpDepthPulse: 10f,
                twistAmplitude: 14f,
                apexXRotationLift: 5f,
                horizontalSpanMultiplier: 1.35f),
            JumpStyle.HighArc => (
                jumpRotationDegrees: 220f,
                jumpDurationSeconds: 2.15f,
                jumpHeight: 215f,
                jumpDepthPulse: 22f,
                twistAmplitude: 0f,
                apexXRotationLift: 12f,
                horizontalSpanMultiplier: 0.8f),
            _ => (
                jumpRotationDegrees: DefaultJumpRotationDegrees,
                jumpDurationSeconds: DefaultJumpDurationSeconds,
                jumpHeight: DefaultJumpHeight,
                jumpDepthPulse: DefaultJumpDepthPulse,
                twistAmplitude: DefaultTwistAmplitude,
                apexXRotationLift: DefaultApexXRotationLift,
                horizontalSpanMultiplier: 1.0f)
        };

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (!theObject.IsOnScreen)
                return theObject;

            ConfigureAudio(audioPlayer, soundRegistry);
            InitializeBaseGeometry(theObject);

            float deltaSeconds = GetDeltaSeconds();
            if (_firstPoseApplied)
            {
                bool wrapped = AdvanceJump(deltaSeconds);
                if (wrapped)
                {
                    AdvanceToNextJumpSegment();
                    _takeoffSplashReleased = false;
                    _landingSplashReleased = false;
                }
            }
            else
            {
                _firstPoseApplied = true;
                _jumpTimeSeconds = _initialPhaseOffsetSeconds;
                bool startsMidJump = _jumpTimeSeconds > _waterDwellSeconds;
                _takeoffSplashReleased = startsMidJump;
                _landingSplashReleased = false;
            }

            ApplyJumpPose(theObject);
            AnchorExistingSplashParticles(theObject);
            if (theObject.IsOnScreen)
            {
                ReleaseSplashParticles(theObject);
                AnimateTailFrames(theObject);
                AnimatePectoralFins(theObject);
                ExplosionParticleHelpers.MoveParticles(theObject);
            }

            SyncToOriginal(theObject);

            return theObject;
        }

        private float GetDeltaSeconds()
        {
            if (GameState.DeltaTime > 0f)
                return Math.Clamp(GameState.DeltaTime, 0f, 0.1f);

            var now = DateTime.Now;
            float deltaSeconds = GameState.GameplayBaselineDeltaTime;

            if (_lastFrameTime != DateTime.MinValue)
                deltaSeconds = (float)(now - _lastFrameTime).TotalSeconds;

            _lastFrameTime = now;
            return Math.Clamp(deltaSeconds, 0f, 0.1f);
        }

        private bool AdvanceJump(float deltaSeconds)
        {
            _jumpTimeSeconds += deltaSeconds;
            bool wrapped = false;
            float cycleSeconds = _jumpDurationSeconds + _waterDwellSeconds;
            while (cycleSeconds > 0f && _jumpTimeSeconds > cycleSeconds)
            {
                _jumpTimeSeconds -= cycleSeconds;
                wrapped = true;
            }

            return wrapped;
        }

        private void AdvanceToNextJumpSegment()
        {
            if (!_hasPathBounds)
            {
                _jumpDirection *= -1;
                return;
            }

            float nextBaseOffsetX = _baseOffsetX + (_jumpDirection * _jumpHorizontalSpan);
            if (CanFitJumpSegment(nextBaseOffsetX))
            {
                _baseOffsetX = nextBaseOffsetX;
                return;
            }

            float currentLandingX = _baseOffsetX + (_jumpDirection * _jumpHorizontalSpan * 0.5f);
            _jumpDirection *= -1;
            _baseOffsetX = currentLandingX + (_jumpDirection * _jumpHorizontalSpan * 0.5f);
        }

        private bool CanFitJumpSegment(float baseOffsetX)
        {
            float halfSpan = _jumpHorizontalSpan * 0.5f;
            return baseOffsetX - halfSpan >= _minPathOffsetX &&
                   baseOffsetX + halfSpan <= _maxPathOffsetX;
        }

        private void ApplyJumpPose(I3dObject theObject)
        {
            float phase = GetJumpPhase();
            float easedPhase = SmoothStep(phase);
            float arc = MathF.Sin(phase * MathF.PI);
            bool isSubmergedDwell = _waterDwellSeconds > 0f && _jumpTimeSeconds < _waterDwellSeconds;

            if (theObject.ObjectOffsets != null)
            {
                theObject.ObjectOffsets = new Vector3
                {
                    x = _baseOffsetX + (_jumpDirection * _jumpHorizontalSpan * (easedPhase - 0.5f)),
                    y = isSubmergedDwell
                        ? _baseOffsetY + SubmergedDwellDepth
                        : _baseOffsetY - (_jumpHeight * arc),
                    z = _baseOffsetZ + (_jumpDepthPulse * arc)
                };
            }

            if (theObject.Rotation != null)
            {
                theObject.Rotation.x = BaseXRotation + (_apexXRotationLift * arc);
                theObject.Rotation.y = BaseYRotation - (_jumpDirection * _twistAmplitude * arc);
                theObject.Rotation.z = StartZRotation + (_jumpDirection * _jumpRotationDegrees * easedPhase);
            }
        }

        private float GetJumpPhase()
        {
            if (_jumpDurationSeconds <= 0f)
                return 0f;

            float activeJumpTime = _jumpTimeSeconds - _waterDwellSeconds;
            if (activeJumpTime <= 0f)
                return 0f;

            return Math.Clamp(activeJumpTime / _jumpDurationSeconds, 0f, 1f);
        }

        private static float SmoothStep(float value)
        {
            value = Math.Clamp(value, 0f, 1f);
            return value * value * (3f - (2f * value));
        }

        private void AnimateTailFrames(I3dObject theObject)
        {
            int frame = (int)(_jumpTimeSeconds * TailFramesPerSecond) % TailFrameCount;
            if (frame < 0) frame += TailFrameCount;

            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName == null)
                    continue;

                if (TryGetTailFrameIndex(part.PartName, out int partFrame))
                    part.IsVisible = partFrame == frame;
            }
        }

        private void AnimatePectoralFins(I3dObject theObject)
        {
            float finAngle = MathF.Sin((_jumpTimeSeconds * FinRadiansPerSecond) + 0.35f) * PectoralFinAmplitude;

            ApplyFinAnimation(theObject, "LeftPectoralFin", LeftFinPivotY, finAngle);
            ApplyFinAnimation(theObject, "RightPectoralFin", RightFinPivotY, -finAngle);
        }

        private void ReleaseSplashParticles(I3dObject theObject)
        {
            float phase = GetJumpPhase();
            bool dwellComplete = _jumpTimeSeconds >= _waterDwellSeconds;
            if (!_takeoffSplashReleased && dwellComplete && phase <= 0.05f)
            {
                ReleaseBlueExplosionParticles(theObject, landingSplash: false);
                _takeoffSplashReleased = true;
            }

            if (!_landingSplashReleased && phase >= LandingSplashPhase)
            {
                ReleaseBlueExplosionParticles(theObject, landingSplash: true);
                PlaySplashSound(theObject);
                _landingSplashReleased = true;
            }
        }

        private void PlaySplashSound(I3dObject theObject)
        {
            if (_audio == null || _splashSound == null) return;

            var audioPosition = ((_3dObject)theObject).GetAudioPosition();
            _audio.Play(
                _splashSound,
                AudioPlayMode.OneShot,
                new AudioPlayOptions
                {
                    WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                });
        }

        private void ReleaseBlueExplosionParticles(I3dObject theObject, bool landingSplash)
        {
            if (theObject.Particles == null)
                return;

            var start = CreateSplashPointTriangle(GetSplashLocalPoint(theObject));
            var guide = CreateSplashPointTriangle(new Vector3
            {
                x = start.vert1.x,
                y = start.vert1.y - 1f,
                z = start.vert1.z
            });

            if (theObject.Particles is ParticlesAI particles)
            {
                string? previousStart = particles.ColorStartOverride;
                string? previousMid = particles.ColorMidOverride;
                string? previousEnd = particles.ColorEndOverride;
                float previousExplosionStartYOffset = particles.ExplosionStartYOffset;

                particles.ColorStartOverride = "d8f6ff";
                particles.ColorMidOverride = "3db7ff";
                particles.ColorEndOverride = "114cbb";
                particles.ExplosionStartYOffset = 0f;
                particles.ReleaseParticles(
                    guide,
                    start,
                    ToVector3(theObject.WorldPosition),
                    this,
                    SplashParticleThrust,
                    true,
                    SplashUpwardVelocityBoost);
                particles.ColorStartOverride = previousStart;
                particles.ColorMidOverride = previousMid;
                particles.ColorEndOverride = previousEnd;
                particles.ExplosionStartYOffset = previousExplosionStartYOffset;
                return;
            }

            theObject.Particles.ReleaseParticles(
                guide,
                start,
                ToVector3(theObject.WorldPosition),
                this,
                SplashParticleThrust,
                true,
                SplashUpwardVelocityBoost);
        }

        private void AnchorExistingSplashParticles(I3dObject theObject)
        {
            var offsets = theObject.ObjectOffsets;
            if (offsets == null)
                return;

            if (!_particleAnchorInitialized)
            {
                _particleAnchorOffsets = ToVector3(offsets);
                _particleAnchorInitialized = true;
                return;
            }

            float deltaX = _particleAnchorOffsets.x - offsets.x;
            float deltaY = _particleAnchorOffsets.y - offsets.y;
            float deltaZ = _particleAnchorOffsets.z - offsets.z;
            if (MathF.Abs(deltaX) < 0.001f && MathF.Abs(deltaY) < 0.001f && MathF.Abs(deltaZ) < 0.001f)
            {
                _particleAnchorOffsets = ToVector3(offsets);
                return;
            }

            var particles = theObject.Particles?.Particles;
            if (particles != null)
            {
                foreach (var particle in particles)
                {
                    if (particle.Position == null)
                        continue;

                    particle.Position.x += deltaX;
                    particle.Position.y += deltaY;
                    particle.Position.z += deltaZ;
                }
            }

            _particleAnchorOffsets = ToVector3(offsets);
        }

        private Vector3 GetSplashLocalPoint(I3dObject theObject)
        {
            var offsets = theObject.ObjectOffsets;

            if (offsets == null)
                return new Vector3 { y = SplashSurfaceLift };

            return new Vector3
            {
                x = 0f,
                y = _baseOffsetY - offsets.y + SplashSurfaceLift,
                z = _baseOffsetZ - offsets.z
            };
        }

        private static ITriangleMeshWithColor CreateSplashPointTriangle(Vector3 point)
        {
            return new TriangleMeshWithColor
            {
                Color = "d8f6ff",
                noHidden = true,
                vert1 = new Vector3 { x = point.x, y = point.y, z = point.z },
                vert2 = new Vector3 { x = point.x, y = point.y, z = point.z },
                vert3 = new Vector3 { x = point.x, y = point.y, z = point.z }
            };
        }

        private static Vector3 ToVector3(IVector3? vector)
        {
            if (vector == null) return new Vector3();
            return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
        }

        private static bool TryGetTailFrameIndex(string partName, out int frame)
        {
            const string tailBasePrefix = "TailBase_Frame";
            const string tailTipPrefix = "TailTip_Frame";

            frame = -1;
            string? frameText = null;
            if (partName.StartsWith(tailBasePrefix, StringComparison.Ordinal))
                frameText = partName.Substring(tailBasePrefix.Length);
            else if (partName.StartsWith(tailTipPrefix, StringComparison.Ordinal))
                frameText = partName.Substring(tailTipPrefix.Length);

            return frameText != null && int.TryParse(frameText, out frame);
        }

        private void ApplyFinAnimation(I3dObject theObject, string partName, float pivotY, float angleDegrees)
        {
            var part = theObject.ObjectParts.Find(p => p.PartName == partName);
            if (part == null) return;
            if (!_baseTrianglesByPart.TryGetValue(partName, out var baseTriangles)) return;

            var shifted = OmegaObjectHelpers.CopyTriangles(baseTriangles);
            TranslateTrianglesYZ(shifted, -pivotY, -FinPivotZ);
            var rotated = _rotate.RotateXMesh(shifted, angleDegrees);
            TranslateTrianglesYZ(rotated, pivotY, FinPivotZ);
            part.Triangles = rotated;
        }

        private void InitializeBaseGeometry(I3dObject theObject)
        {
            if (_baseInitialized)
                return;

            _baseTrianglesByPart.Clear();
            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName == "LeftPectoralFin" || part.PartName == "RightPectoralFin")
                    _baseTrianglesByPart[part.PartName] = OmegaObjectHelpers.CopyTriangles(part.Triangles);
            }

            _baseOffsetX = theObject.ObjectOffsets?.x ?? 0f;
            _baseOffsetY = theObject.ObjectOffsets?.y ?? 0f;
            _baseOffsetZ = theObject.ObjectOffsets?.z ?? 0f;
            if (_hasPathBounds)
                _baseOffsetX = _jumpDirection < 0
                    ? _maxPathOffsetX - (_jumpHorizontalSpan * 0.5f)
                    : _minPathOffsetX + (_jumpHorizontalSpan * 0.5f);

            _baseInitialized = true;
        }

        private static void SyncToOriginal(I3dObject source)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null) return;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectId != source.ObjectId)
                    continue;

                var original = aiObjects[i];
                if (ReferenceEquals(original, source)) return;

                if (source.WorldPosition != null)
                {
                    original.WorldPosition = new Vector3
                    {
                        x = source.WorldPosition.x,
                        y = source.WorldPosition.y,
                        z = source.WorldPosition.z
                    };
                }

                if (source.ObjectOffsets != null)
                {
                    original.ObjectOffsets = new Vector3
                    {
                        x = source.ObjectOffsets.x,
                        y = source.ObjectOffsets.y,
                        z = source.ObjectOffsets.z
                    };
                }

                return;
            }
        }

        private static void TranslateTrianglesYZ(List<ITriangleMeshWithColor> triangles, float dy, float dz)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                var t = triangles[i];
                t.vert1.y += dy; t.vert1.z += dz;
                t.vert2.y += dy; t.vert2.z += dz;
                t.vert3.y += dy; t.vert3.z += dz;
            }
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured) return;
            if (audioPlayer == null || soundRegistry == null) return;

            _audio = audioPlayer;
            soundRegistry.TryGet("water_splash", out _splashSound);
            _audioConfigured = true;
        }

        public void ReleaseParticles(I3dObject theObject)
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void Dispose()
        {
            _baseTrianglesByPart.Clear();
            _lastFrameTime = DateTime.MinValue;
            _jumpTimeSeconds = 0f;
            _baseOffsetX = 0f;
            _baseOffsetY = 0f;
            _baseOffsetZ = 0f;
            _baseInitialized = false;
            _firstPoseApplied = false;
            _takeoffSplashReleased = false;
            _landingSplashReleased = false;
            _particleAnchorInitialized = false;
            _particleAnchorOffsets = new Vector3();
            _jumpDirection = _initialJumpDirection;
            StartCoordinates = null;
            GuideCoordinates = null;
        }
    }
}
