using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using CommonUtilities.OmegaEngineAdapters;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public class OutroAstronautControls : IObjectMovement
    {
        public const float RevealScaleSeconds = 1f;

        private const float WaveSpeedRadiansPerSecond = 5.2f;
        private const float WaveBaseDegrees = -12f;
        private const float WaveAmplitudeDegrees = 20f;
        private const float RevealStartScale = 0.42f;
        private const float RevealStartYScale = 0.08f;
        private static readonly Vector3 ShoulderPivot = new Vector3 { x = 8f, y = -12f, z = 23f };

        private readonly string _wavingArmPartName;
        private List<PartBasePose>? _baseParts;
        private Vector3 _revealCenter;
        private float _timeSeconds;
        private float _revealElapsedSeconds;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = null!;

        public OutroAstronautControls(string wavingArmPartName)
        {
            _wavingArmPartName = wavingArmPartName;
        }

        public void PrepareInitialPose(I3dObject theObject)
        {
            CaptureBasePose(theObject);
            ApplyRevealPose(0f);
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            CaptureBasePose(theObject);

            float deltaSeconds = GetDeltaSeconds();
            _timeSeconds += deltaSeconds;
            _revealElapsedSeconds = Math.Min(RevealScaleSeconds, _revealElapsedSeconds + deltaSeconds);

            float revealProgress = SmoothStep(_revealElapsedSeconds / RevealScaleSeconds);
            ApplyRevealPose(revealProgress);
            return theObject;
        }

        private void CaptureBasePose(I3dObject theObject)
        {
            if (_baseParts != null)
                return;

            _baseParts = new List<PartBasePose>(theObject.ObjectParts.Count);
            foreach (var part in theObject.ObjectParts)
            {
                _baseParts.Add(new PartBasePose(part, OmegaObjectHelpers.CopyTriangles(part.Triangles)));
            }

            _revealCenter = CalculateCenter(_baseParts);
        }

        private void ApplyRevealPose(float revealProgress)
        {
            if (_baseParts == null)
                return;

            float scale = Lerp(RevealStartScale, 1f, revealProgress);
            float yScale = scale * Lerp(RevealStartYScale, 1f, revealProgress);
            float waveAngle = WaveBaseDegrees + (MathF.Sin(_timeSeconds * WaveSpeedRadiansPerSecond) * WaveAmplitudeDegrees);
            var wavePivot = TransformForReveal(ShoulderPivot, scale, yScale);

            for (int i = 0; i < _baseParts.Count; i++)
            {
                var basePart = _baseParts[i];
                var transformed = TransformTrianglesForReveal(basePart.Triangles, scale, yScale);
                if (basePart.Part.PartName == _wavingArmPartName)
                    transformed = RotateAroundPivotY(transformed, wavePivot, waveAngle);

                basePart.Part.Triangles = transformed;
            }
        }

        private List<ITriangleMeshWithColor> TransformTrianglesForReveal(List<ITriangleMeshWithColor> source, float scale, float yScale)
        {
            var transformed = new List<ITriangleMeshWithColor>(source.Count);
            foreach (var triangle in source)
            {
                transformed.Add(new TriangleMeshWithColor
                {
                    Color = triangle.Color,
                    noHidden = triangle.noHidden,
                    landBasedPosition = triangle.landBasedPosition,
                    angle = triangle.angle,
                    normal1 = CopyVector(triangle.normal1),
                    normal2 = CopyVector(triangle.normal2),
                    normal3 = CopyVector(triangle.normal3),
                    vert1 = TransformForReveal(triangle.vert1, scale, yScale),
                    vert2 = TransformForReveal(triangle.vert2, scale, yScale),
                    vert3 = TransformForReveal(triangle.vert3, scale, yScale)
                });
            }

            return transformed;
        }

        private Vector3 TransformForReveal(IVector3 vertex, float scale, float yScale)
        {
            return new Vector3
            {
                x = _revealCenter.x + ((vertex.x - _revealCenter.x) * scale),
                y = _revealCenter.y + ((vertex.y - _revealCenter.y) * yScale),
                z = _revealCenter.z + ((vertex.z - _revealCenter.z) * scale)
            };
        }

        private static List<ITriangleMeshWithColor> RotateAroundPivotY(List<ITriangleMeshWithColor> source, Vector3 pivot, float angleDegrees)
        {
            var result = OmegaObjectHelpers.CopyTriangles(source);
            float radians = angleDegrees * MathF.PI / 180f;
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);

            for (int i = 0; i < result.Count; i++)
            {
                var triangle = result[i];
                triangle.vert1 = RotatePointY(triangle.vert1, pivot, cos, sin);
                triangle.vert2 = RotatePointY(triangle.vert2, pivot, cos, sin);
                triangle.vert3 = RotatePointY(triangle.vert3, pivot, cos, sin);
                result[i] = triangle;
            }

            return result;
        }

        private static Vector3 CalculateCenter(List<PartBasePose> parts)
        {
            float x = 0f;
            float y = 0f;
            float z = 0f;
            int count = 0;

            for (int i = 0; i < parts.Count; i++)
            {
                foreach (var triangle in parts[i].Triangles)
                {
                    Add(triangle.vert1);
                    Add(triangle.vert2);
                    Add(triangle.vert3);
                }
            }

            return count == 0
                ? new Vector3()
                : new Vector3 { x = x / count, y = y / count, z = z / count };

            void Add(IVector3 vertex)
            {
                x += vertex.x;
                y += vertex.y;
                z += vertex.z;
                count++;
            }
        }

        private static float SmoothStep(float value)
        {
            value = Math.Clamp(value, 0f, 1f);
            return value * value * (3f - (2f * value));
        }

        private static float Lerp(float from, float to, float amount)
        {
            return from + ((to - from) * amount);
        }

        private static Vector3 RotatePointY(IVector3 vertex, Vector3 pivot, float cos, float sin)
        {
            float dx = vertex.x - pivot.x;
            float dz = vertex.z - pivot.z;
            return new Vector3
            {
                x = pivot.x + (dx * cos) + (dz * sin),
                y = vertex.y,
                z = pivot.z + (dz * cos) - (dx * sin)
            };
        }

        private static float GetDeltaSeconds()
        {
            if (GameState.DeltaTime > 0f)
                return Math.Min(GameState.DeltaTime, 0.1f);

            return GameState.GameplayBaselineDeltaTime;
        }

        private static Vector3 CopyVector(IVector3 vector)
        {
            return new Vector3
            {
                x = vector?.x ?? 0f,
                y = vector?.y ?? 0f,
                z = vector?.z ?? 0f
            };
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void Dispose() => _baseParts = null;

        private sealed class PartBasePose
        {
            public PartBasePose(I3dObjectPart part, List<ITriangleMeshWithColor> triangles)
            {
                Part = part;
                Triangles = triangles;
            }

            public I3dObjectPart Part { get; }
            public List<ITriangleMeshWithColor> Triangles { get; }
        }
    }
}
