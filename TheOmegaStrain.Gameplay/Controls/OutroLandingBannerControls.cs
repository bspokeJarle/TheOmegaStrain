using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class OutroLandingBannerControls : IObjectMovement
    {
        private const float WaveSpeedRadiansPerSecond = 1.65f;
        private const float WaveLengthCycles = 1.55f;
        private const float WaveAmplitudeY = 9f;
        private const float WaveAmplitudeZ = 13f;

        private readonly Dictionary<string, List<ITriangleMeshWithColor>> _baseTrianglesByPart = new();
        private bool _baseInitialized;
        private float _timeSeconds;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = null!;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            InitializeBaseTriangles(theObject);
            _timeSeconds += GetDeltaSeconds();

            float minX = GetBannerMinX();
            float maxX = GetBannerMaxX();
            float width = Math.Max(1f, maxX - minX);

            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName == null || !part.PartName.StartsWith("BannerSegment_", StringComparison.Ordinal))
                    continue;

                if (!_baseTrianglesByPart.TryGetValue(part.PartName, out var baseTriangles))
                    continue;

                part.Triangles = CreateWaveTriangles(baseTriangles, minX, width);
            }

            return theObject;
        }

        private List<ITriangleMeshWithColor> CreateWaveTriangles(List<ITriangleMeshWithColor> baseTriangles, float minX, float width)
        {
            var animated = new List<ITriangleMeshWithColor>(baseTriangles.Count);
            foreach (var triangle in baseTriangles)
            {
                animated.Add(new TriangleMeshWithColor
                {
                    Color = triangle.Color,
                    noHidden = triangle.noHidden,
                    landBasedPosition = triangle.landBasedPosition,
                    angle = triangle.angle,
                    normal1 = CopyVector(triangle.normal1),
                    normal2 = CopyVector(triangle.normal2),
                    normal3 = CopyVector(triangle.normal3),
                    vert1 = AnimateVertex(triangle.vert1, minX, width),
                    vert2 = AnimateVertex(triangle.vert2, minX, width),
                    vert3 = AnimateVertex(triangle.vert3, minX, width)
                });
            }

            return animated;
        }

        private Vector3 AnimateVertex(IVector3 vertex, float minX, float width)
        {
            float normalized = Math.Clamp((vertex.x - minX) / width, 0f, 1f);
            float envelope = MathF.Sin(normalized * MathF.PI);
            float phase = (_timeSeconds * WaveSpeedRadiansPerSecond) + (normalized * MathF.PI * 2f * WaveLengthCycles);
            float yOffset = MathF.Sin(phase) * WaveAmplitudeY * envelope;
            float zOffset = MathF.Cos(phase) * WaveAmplitudeZ * envelope;

            return new Vector3
            {
                x = vertex.x,
                y = vertex.y + yOffset,
                z = vertex.z + zOffset
            };
        }

        private float GetBannerMinX()
        {
            float minX = float.MaxValue;
            foreach (var entry in _baseTrianglesByPart)
                if (entry.Key.StartsWith("BannerSegment_", StringComparison.Ordinal))
                    UpdateMinX(entry.Value, ref minX);

            return minX == float.MaxValue ? -1f : minX;
        }

        private float GetBannerMaxX()
        {
            float maxX = float.MinValue;
            foreach (var entry in _baseTrianglesByPart)
                if (entry.Key.StartsWith("BannerSegment_", StringComparison.Ordinal))
                    UpdateMaxX(entry.Value, ref maxX);

            return maxX == float.MinValue ? 1f : maxX;
        }

        private static void UpdateMinX(List<ITriangleMeshWithColor> triangles, ref float minX)
        {
            foreach (var triangle in triangles)
            {
                minX = Math.Min(minX, triangle.vert1.x);
                minX = Math.Min(minX, triangle.vert2.x);
                minX = Math.Min(minX, triangle.vert3.x);
            }
        }

        private static void UpdateMaxX(List<ITriangleMeshWithColor> triangles, ref float maxX)
        {
            foreach (var triangle in triangles)
            {
                maxX = Math.Max(maxX, triangle.vert1.x);
                maxX = Math.Max(maxX, triangle.vert2.x);
                maxX = Math.Max(maxX, triangle.vert3.x);
            }
        }

        private void InitializeBaseTriangles(I3dObject theObject)
        {
            if (_baseInitialized)
                return;

            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName == null)
                    continue;

                _baseTrianglesByPart[part.PartName] = OmegaObjectHelpers.CopyTriangles(part.Triangles);
            }

            _baseInitialized = true;
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
        public void Dispose() => _baseTrianglesByPart.Clear();
    }
}
