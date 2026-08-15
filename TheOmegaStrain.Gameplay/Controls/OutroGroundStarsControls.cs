using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class OutroGroundStarsControls : IObjectMovement
    {
        private const int TrianglesPerStar = 4;
        private const float PulseSpeedRadiansPerSecond = 2.1f;
        private const float ScaleAmplitude = 0.22f;
        private const float DriftAmplitude = 2.5f;

        private List<ITriangleMeshWithColor>? _baseTriangles;
        private float _timeSeconds;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = null!;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            var part = theObject.ObjectParts.Count > 0 ? theObject.ObjectParts[0] : null;
            if (part == null)
                return theObject;

            _baseTriangles ??= OmegaObjectHelpers.CopyTriangles(part.Triangles);
            _timeSeconds += GetDeltaSeconds();
            part.Triangles = CreatePulsingStars(_baseTriangles);
            return theObject;
        }

        private List<ITriangleMeshWithColor> CreatePulsingStars(List<ITriangleMeshWithColor> baseTriangles)
        {
            var animated = new List<ITriangleMeshWithColor>(baseTriangles.Count);
            int starIndex = 0;
            for (int i = 0; i < baseTriangles.Count; i += TrianglesPerStar)
            {
                int count = Math.Min(TrianglesPerStar, baseTriangles.Count - i);
                var center = GetStarCenter(baseTriangles, i, count);
                float phase = _timeSeconds * PulseSpeedRadiansPerSecond + (starIndex * 0.73f);
                float pulse = (MathF.Sin(phase) + 1f) * 0.5f;
                float scale = 1f - (ScaleAmplitude * 0.45f) + (pulse * ScaleAmplitude);
                float driftX = MathF.Sin(phase * 0.71f) * DriftAmplitude;
                float driftY = MathF.Cos(phase * 0.59f) * DriftAmplitude;
                string color = CreatePulseColor(pulse);

                for (int t = 0; t < count; t++)
                {
                    var triangle = baseTriangles[i + t];
                    animated.Add(new TriangleMeshWithColor
                    {
                        Color = color,
                        noHidden = triangle.noHidden,
                        landBasedPosition = triangle.landBasedPosition,
                        angle = triangle.angle,
                        normal1 = CopyVector(triangle.normal1),
                        normal2 = CopyVector(triangle.normal2),
                        normal3 = CopyVector(triangle.normal3),
                        vert1 = PulseVertex(triangle.vert1, center, scale, driftX, driftY),
                        vert2 = PulseVertex(triangle.vert2, center, scale, driftX, driftY),
                        vert3 = PulseVertex(triangle.vert3, center, scale, driftX, driftY)
                    });
                }

                starIndex++;
            }

            return animated;
        }

        private static Vector3 GetStarCenter(List<ITriangleMeshWithColor> triangles, int startIndex, int count)
        {
            float x = 0f;
            float y = 0f;
            float z = 0f;
            int vertexCount = 0;
            for (int i = startIndex; i < startIndex + count; i++)
            {
                Add(triangles[i].vert1);
                Add(triangles[i].vert2);
                Add(triangles[i].vert3);
            }

            return new Vector3
            {
                x = x / vertexCount,
                y = y / vertexCount,
                z = z / vertexCount
            };

            void Add(IVector3 vertex)
            {
                x += vertex.x;
                y += vertex.y;
                z += vertex.z;
                vertexCount++;
            }
        }

        private static Vector3 PulseVertex(IVector3 vertex, Vector3 center, float scale, float driftX, float driftY)
        {
            return new Vector3
            {
                x = center.x + ((vertex.x - center.x) * scale) + driftX,
                y = center.y + ((vertex.y - center.y) * scale) + driftY,
                z = vertex.z
            };
        }

        private static string CreatePulseColor(float pulse)
        {
            int warm = 205 + (int)(pulse * 50f);
            int blue = 225 + (int)(pulse * 30f);
            return $"{warm:X2}{warm:X2}{blue:X2}";
        }

        private static float GetDeltaSeconds()
        {
            if (GameState.DeltaTime > 0f)
                return Math.Min(GameState.DeltaTime, 0.1f);

            return GameState.GameplayBaselineDeltaTime;
        }

        private static Vector3 CopyVector(IVector3 vector)
        {
            return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void Dispose() => _baseTriangles = null;
    }
}
