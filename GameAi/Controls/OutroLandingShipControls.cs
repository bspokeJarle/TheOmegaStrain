using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using NumericsVector3 = System.Numerics.Vector3;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public class OutroLandingShipControls : IObjectMovement
    {
        public const float LandingSeconds = 3.2f;
        public const float LandingZSortBias = 560f;
        public const int ParticleEmissionFrameInterval = 5;
        private const float ParticleEmissionIntervalSeconds = ParticleEmissionFrameInterval / GameState.GameplayBaselineFps;
        public const float LandingVisualScale = 1.24f;
        public const float UpperHatchOpenSeconds = 0.9f;
        public const float UpperHatchOpenDegrees = -72f;
        public const float AstronautRevealDelayAfterLandingSeconds = 0.25f;

        private const int LowerEngineThrust = 4;
        private const string LowerEngineActiveColor = "FFE46A";
        private const string RearEngineIdleColor = "551000";
        private const float StartTurnZDegrees = 0f;
        private const float LandingTurnZDegrees = 180f;
        private const float BasePitchXDegrees = WorldViewSetup.CameraPitchDegrees;
        private const float LandingPitchXDegrees = 84f;
        private const float FinalPoseStartProgress = 0.74f;
        private const string UpperHatchPartName = "UpperPart";
        private const string LowerHullPartName = "LowerPart";
        private const string TopCannonPartName = "TopCannon";

        private float _particleEmissionSeconds = ParticleEmissionIntervalSeconds;
        private float _elapsedSeconds;
        private bool _hasInitialized;
        private readonly Vector3 _initialOffset;
        private readonly Vector3 _finalOffset;
        private List<List<BaselineTriangle>>? _baselineGeometry;
        private Vector3 _baselineCenter;
        private Vector3 _upperHatchHinge;
        private bool _hasUpperHatchHinge;

        private bool _audioConfigured;
        private IAudioPlayer? _audio;
        private SoundDefinition? _rocketSound;
        private IAudioInstance? _rocketInstance;
        private bool _rocketStopRequested;

        public I3dObject? ParentObject { get; set; }
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();
        public bool IsLanded => _elapsedSeconds >= LandingSeconds;
        public bool IsHatchOpen => _elapsedSeconds >= LandingSeconds + UpperHatchOpenSeconds;
        public bool IsAstronautRevealReady => _elapsedSeconds >= LandingSeconds + AstronautRevealDelayAfterLandingSeconds;

        public OutroLandingShipControls(Vector3 initialOffset, Vector3 finalOffset)
        {
            _initialOffset = CopyVector(initialOffset);
            _finalOffset = CopyVector(finalOffset);
        }

        public static Vector3 CreateInitialLandingRotation()
        {
            return CreateLandingRotation(StartTurnZDegrees);
        }

        public static Vector3 CreateFinalLandingRotation()
        {
            return CreateLandingRotation(LandingPitchXDegrees, LandingTurnZDegrees);
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ConfigureAudio(audioPlayer, soundRegistry);
            ParentObject = theObject;

            if (!_hasInitialized)
                InitializeShip(theObject);

            if (!IsHatchOpen)
                _elapsedSeconds = Math.Min(_elapsedSeconds + GetDeltaSeconds(), LandingSeconds + UpperHatchOpenSeconds);

            float linearProgress = Math.Clamp(_elapsedSeconds / LandingSeconds, 0f, 1f);
            float progress = SmoothStep(linearProgress);
            float finalPoseProgress = GetFinalPoseProgress(linearProgress);
            float upperHatchProgress = GetUpperHatchProgress(_elapsedSeconds);
            theObject.ObjectOffsets = LerpOffset(_initialOffset, _finalOffset, progress);

            theObject.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
            theObject.Rotation = CreateLandingRotation(
                Lerp(BasePitchXDegrees, LandingPitchXDegrees, finalPoseProgress),
                Lerp(StartTurnZDegrees, LandingTurnZDegrees, progress));
            theObject.ZSortBias = LandingZSortBias;
            ApplyLandingPose(theObject, finalPoseProgress, upperHatchProgress);
            SetPartColor(theObject, "JetMotor", LowerEngineActiveColor);
            SetPartColor(theObject, "RearEngine", RearEngineIdleColor);

            if (IsLanded)
                StopRocket(playEndSegment: true);
            else
                EnsureRocketLoop(theObject);

            ReleaseParticles(theObject);

            return theObject;
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            if (theObject.Particles == null)
                return;

            ConfigureParticleStyle(theObject);

            if (StartCoordinates == null || GuideCoordinates == null)
            {
                theObject.Particles.MoveParticles();
                return;
            }

            _particleEmissionSeconds += GameState.ClampedDeltaTime;
            bool shouldEmit = !IsLanded && _particleEmissionSeconds >= ParticleEmissionIntervalSeconds;
            if (shouldEmit)
            {
                theObject.Particles.ReleaseParticles(
                    GuideCoordinates,
                    StartCoordinates,
                    new Vector3 { x = 0f, y = 0f, z = 0f },
                    this,
                    LowerEngineThrust,
                    false);
                _particleEmissionSeconds = 0f;
            }
            theObject.Particles.MoveParticles();
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured)
                return;

            if (audioPlayer == null || soundRegistry == null)
                return;

            _audio = audioPlayer;
            if (soundRegistry.TryGet("rocket_main", out var rocketSound))
                _rocketSound = rocketSound;

            _audioConfigured = true;
        }
        public void Dispose()
        {
            StopRocket(playEndSegment: false);
        }

        private void InitializeShip(I3dObject theObject)
        {
            _hasInitialized = true;
            theObject.IsActive = true;
            theObject.ObjectOffsets = CopyVector(_initialOffset);
            theObject.WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f };
            theObject.Rotation = CreateInitialLandingRotation();
            theObject.ZSortBias = LandingZSortBias;
            _particleEmissionSeconds = ParticleEmissionIntervalSeconds;
            ConfigureParticleStyle(theObject);
            CaptureBaselineGeometry(theObject);
            ApplyLandingPose(theObject, 0f, 0f);
        }

        private static Vector3 CreateLandingRotation(float zDegrees)
        {
            return CreateLandingRotation(BasePitchXDegrees, zDegrees);
        }

        private static Vector3 CreateLandingRotation(float xDegrees, float zDegrees)
        {
            return new Vector3 { x = xDegrees, y = 0, z = zDegrees };
        }

        private static void ConfigureParticleStyle(I3dObject theObject)
        {
            if (theObject.Particles is not ParticlesAI particles)
                return;

            particles.MaxParticlesOverride = 90;
            particles.LifeMultiplier = 0.7f;
            particles.ColorStartOverride = "fff6a0";
            particles.ColorMidOverride = "ff9a2c";
            particles.ColorEndOverride = "6b2b00";
        }

        private static float GetDeltaSeconds()
        {
            if (GameState.DeltaTime > 0f)
                return Math.Min(GameState.DeltaTime, 0.1f);

            return GameState.GameplayBaselineDeltaTime;
        }

        private static float SmoothStep(float value)
        {
            value = Math.Clamp(value, 0f, 1f);
            return value * value * (3f - (2f * value));
        }

        private static float GetFinalPoseProgress(float linearProgress)
        {
            return SmoothStep((linearProgress - FinalPoseStartProgress) / (1f - FinalPoseStartProgress));
        }

        private static float GetUpperHatchProgress(float elapsedSeconds)
        {
            return SmoothStep((elapsedSeconds - LandingSeconds) / UpperHatchOpenSeconds);
        }

        private void CaptureBaselineGeometry(I3dObject theObject)
        {
            if (_baselineGeometry != null)
                return;

            _baselineGeometry = new List<List<BaselineTriangle>>(theObject.ObjectParts.Count);
            float sumX = 0f;
            float sumY = 0f;
            float sumZ = 0f;
            int vertexCount = 0;

            foreach (var part in theObject.ObjectParts)
            {
                var partBaseline = new List<BaselineTriangle>(part.Triangles.Count);
                foreach (var triangle in part.Triangles)
                {
                    var baseline = new BaselineTriangle
                    {
                        V1 = CopyVector(triangle.vert1),
                        V2 = CopyVector(triangle.vert2),
                        V3 = CopyVector(triangle.vert3)
                    };

                    partBaseline.Add(baseline);
                    AddToCenterSum(baseline.V1, ref sumX, ref sumY, ref sumZ, ref vertexCount);
                    AddToCenterSum(baseline.V2, ref sumX, ref sumY, ref sumZ, ref vertexCount);
                    AddToCenterSum(baseline.V3, ref sumX, ref sumY, ref sumZ, ref vertexCount);
                }

                _baselineGeometry.Add(partBaseline);

                if (part.PartName == UpperHatchPartName)
                    CaptureUpperHatchHinge(partBaseline);
            }

            if (vertexCount > 0)
            {
                _baselineCenter = new Vector3
                {
                    x = sumX / vertexCount,
                    y = sumY / vertexCount,
                    z = sumZ / vertexCount
                };
            }
        }

        private void ApplyLandingPose(I3dObject theObject, float finalPoseProgress, float upperHatchProgress)
        {
            if (_baselineGeometry == null)
                return;

            float scale = Lerp(1f, LandingVisualScale, finalPoseProgress);
            float upperHatchAngle = UpperHatchOpenDegrees * upperHatchProgress;
            var scaledHinge = ScaleFromBaseline(_upperHatchHinge, scale);

            int partCount = Math.Min(theObject.ObjectParts.Count, _baselineGeometry.Count);
            for (int partIndex = 0; partIndex < partCount; partIndex++)
            {
                var part = theObject.ObjectParts[partIndex];
                var baselinePart = _baselineGeometry[partIndex];
                int triangleCount = Math.Min(part.Triangles.Count, baselinePart.Count);

                for (int triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
                {
                    var triangle = part.Triangles[triangleIndex];
                    var baseline = baselinePart[triangleIndex];
                    triangle.vert1 = ScaleFromBaseline(baseline.V1, scale);
                    triangle.vert2 = ScaleFromBaseline(baseline.V2, scale);
                    triangle.vert3 = ScaleFromBaseline(baseline.V3, scale);
                    if (upperHatchProgress > 0f && ShouldRenderNoHiddenDuringHatch(part.PartName))
                    {
                        triangle.noHidden = true;
                    }

                    if (upperHatchProgress > 0f && ShouldMoveWithUpperHatch(part.PartName))
                    {
                        triangle.vert1 = RotateAroundHingeX((Vector3)triangle.vert1, scaledHinge, upperHatchAngle);
                        triangle.vert2 = RotateAroundHingeX((Vector3)triangle.vert2, scaledHinge, upperHatchAngle);
                        triangle.vert3 = RotateAroundHingeX((Vector3)triangle.vert3, scaledHinge, upperHatchAngle);
                    }
                    part.Triangles[triangleIndex] = triangle;
                }
            }
        }

        private void CaptureUpperHatchHinge(List<BaselineTriangle> upperPartBaseline)
        {
            if (upperPartBaseline.Count == 0)
                return;

            float maxY = float.MinValue;
            foreach (var baseline in upperPartBaseline)
            {
                maxY = Math.Max(maxY, baseline.V1.y);
                maxY = Math.Max(maxY, baseline.V2.y);
                maxY = Math.Max(maxY, baseline.V3.y);
            }

            float hingeZ = float.MinValue;
            const float rearVertexTolerance = 2f;
            foreach (var baseline in upperPartBaseline)
            {
                CaptureRearHingeZ(baseline.V1, maxY, rearVertexTolerance, ref hingeZ);
                CaptureRearHingeZ(baseline.V2, maxY, rearVertexTolerance, ref hingeZ);
                CaptureRearHingeZ(baseline.V3, maxY, rearVertexTolerance, ref hingeZ);
            }

            if (hingeZ <= float.MinValue)
                return;

            _upperHatchHinge = new Vector3
            {
                x = 0f,
                y = maxY,
                z = hingeZ
            };
            _hasUpperHatchHinge = true;
        }

        private static void CaptureRearHingeZ(Vector3 vertex, float maxY, float tolerance, ref float hingeZ)
        {
            if (vertex.y >= maxY - tolerance)
                hingeZ = Math.Max(hingeZ, vertex.z);
        }

        private static bool ShouldMoveWithUpperHatch(string? partName)
        {
            return partName == UpperHatchPartName || partName == TopCannonPartName;
        }

        private static bool ShouldRenderNoHiddenDuringHatch(string? partName)
        {
            return ShouldMoveWithUpperHatch(partName) || partName == LowerHullPartName;
        }

        private Vector3 RotateAroundHingeX(Vector3 vertex, Vector3 hinge, float angleDegrees)
        {
            if (!_hasUpperHatchHinge)
                return vertex;

            float radians = angleDegrees * MathF.PI / 180f;
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            float dy = vertex.y - hinge.y;
            float dz = vertex.z - hinge.z;

            return new Vector3
            {
                x = vertex.x,
                y = hinge.y + (dy * cos) - (dz * sin),
                z = hinge.z + (dz * cos) + (dy * sin)
            };
        }

        private Vector3 ScaleFromBaseline(Vector3 vertex, float scale)
        {
            return new Vector3
            {
                x = _baselineCenter.x + ((vertex.x - _baselineCenter.x) * scale),
                y = _baselineCenter.y + ((vertex.y - _baselineCenter.y) * scale),
                z = _baselineCenter.z + ((vertex.z - _baselineCenter.z) * scale)
            };
        }

        private static Vector3 LerpOffset(Vector3 from, Vector3 to, float amount)
        {
            return new Vector3
            {
                x = Lerp(from.x, to.x, amount),
                y = Lerp(from.y, to.y, amount),
                z = Lerp(from.z, to.z, amount)
            };
        }

        private static float Lerp(float from, float to, float amount)
        {
            return from + ((to - from) * amount);
        }

        private static Vector3 CopyVector(Vector3 vector)
        {
            return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
        }

        private static Vector3 CopyVector(IVector3 vector)
        {
            return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
        }

        private static void AddToCenterSum(Vector3 vertex, ref float sumX, ref float sumY, ref float sumZ, ref int count)
        {
            sumX += vertex.x;
            sumY += vertex.y;
            sumZ += vertex.z;
            count++;
        }

        private static void SetPartColor(I3dObject theObject, string partName, string color)
        {
            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName != partName)
                    continue;

                for (int i = 0; i < part.Triangles.Count; i++)
                {
                    var triangle = part.Triangles[i];
                    triangle.Color = color;
                    part.Triangles[i] = triangle;
                }

                return;
            }
        }

        private void EnsureRocketLoop(I3dObject theObject)
        {
            if (_rocketStopRequested || _audio == null || _rocketSound == null || theObject is not _3dObject concrete)
                return;

            var audioPosition = concrete.GetAudioPosition();
            var numericsPosition = new NumericsVector3(audioPosition.x, audioPosition.y, audioPosition.z);

            if (_rocketInstance == null || !_rocketInstance.IsPlaying)
            {
                _rocketInstance = _audio.Play(
                    _rocketSound,
                    AudioPlayMode.SegmentedLoop,
                    new AudioPlayOptions
                    {
                        VolumeOverride = _rocketSound.Settings.Volume,
                        SpeedOverride = Math.Clamp(_rocketSound.Speed.Base * 1.08f, _rocketSound.Speed.Min, _rocketSound.Speed.Max),
                        WorldPosition = numericsPosition
                    });
                return;
            }

            _rocketInstance.SetWorldPosition(numericsPosition);
        }

        private void StopRocket(bool playEndSegment)
        {
            _rocketStopRequested = true;
            if (_rocketInstance == null)
                return;

            _rocketInstance.Stop(playEndSegment);
            _rocketInstance = null;
        }

        private sealed class BaselineTriangle
        {
            public Vector3 V1 { get; init; }
            public Vector3 V2 { get; init; }
            public Vector3 V3 { get; init; }
        }
    }
}
