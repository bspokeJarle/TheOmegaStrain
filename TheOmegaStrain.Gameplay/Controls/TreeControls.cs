using TheOmegaStrain.Domain;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonSetup;
using System;
using System.Collections.Generic;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class TreeControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private const float Yrotation = 0f;
        private const float Xrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private const float Zrotation = 0f;
        private const float WindRadiansPerSecond = 2.05f;
        private const float TrunkAmplitude = 1.1f;
        private const float FoliageAmplitude = 7.0f;
        private const float SecondarySwayScale = 0.45f;
        private const float HeightWeightPower = 1.35f;

        private readonly Dictionary<string, List<ITriangleMeshWithColor>> _baseTrianglesByPart = new();
        private DateTime _lastFrameTime = DateTime.MinValue;
        private float _windTime;
        private float _minTreeZ;
        private float _maxTreeZ = 1f;
        private bool _baseInitialized;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            if (theObject.Rotation != null) theObject.Rotation.y = Yrotation;
            if (theObject.Rotation != null) theObject.Rotation.x = Xrotation;
            if (theObject.Rotation != null) theObject.Rotation.z = Zrotation;

            InitializeBaseTriangles(theObject);
            AnimateWind(theObject);
            return theObject;
        }

        private void AnimateWind(I3dObject theObject)
        {
            var now = DateTime.Now;
            float deltaSeconds = 0f;
            if (_lastFrameTime != DateTime.MinValue)
            {
                deltaSeconds = (float)(now - _lastFrameTime).TotalSeconds;
                deltaSeconds = Math.Clamp(deltaSeconds, 0f, 0.1f);
            }
            _lastFrameTime = now;
            _windTime += deltaSeconds;

            float treePhase = (theObject.ObjectId % 97) * 0.137f;

            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName == null) continue;
                if (!_baseTrianglesByPart.TryGetValue(part.PartName, out var baseTriangles)) continue;

                float amplitude = part.PartName switch
                {
                    "TreeFoliage" => FoliageAmplitude,
                    "TreeTrunk" => TrunkAmplitude,
                    _ => 0f
                };
                if (amplitude <= 0f) continue;

                float partPhase = part.PartName == "TreeFoliage" ? 0.18f : 0f;
                part.Triangles = CreateWindTriangles(baseTriangles, amplitude, treePhase + partPhase);
            }
        }

        private List<ITriangleMeshWithColor> CreateWindTriangles(
            List<ITriangleMeshWithColor> baseTriangles,
            float amplitude,
            float phase)
        {
            var animated = new List<ITriangleMeshWithColor>(baseTriangles.Count);
            float treeHeight = Math.Max(1f, _maxTreeZ - _minTreeZ);
            float mainSway = MathF.Sin((_windTime * WindRadiansPerSecond) + phase);
            float secondarySway = MathF.Sin((_windTime * WindRadiansPerSecond * 0.72f) + phase + 0.65f);

            foreach (var triangle in baseTriangles)
            {
                animated.Add(new TriangleMeshWithColor
                {
                    Color = triangle.Color,
                    noHidden = triangle.noHidden,
                    landBasedPosition = triangle.landBasedPosition,
                    angle = triangle.angle,
                    vert1 = AnimateVertex((Vector3)triangle.vert1, amplitude, mainSway, secondarySway, treeHeight),
                    vert2 = AnimateVertex((Vector3)triangle.vert2, amplitude, mainSway, secondarySway, treeHeight),
                    vert3 = AnimateVertex((Vector3)triangle.vert3, amplitude, mainSway, secondarySway, treeHeight),
                    normal1 = CopyVector(triangle.normal1),
                    normal2 = CopyVector(triangle.normal2),
                    normal3 = CopyVector(triangle.normal3)
                });
            }

            return animated;
        }

        private Vector3 AnimateVertex(Vector3 vertex, float amplitude, float mainSway, float secondarySway, float treeHeight)
        {
            float heightWeight = Math.Clamp((vertex.z - _minTreeZ) / treeHeight, 0f, 1f);
            heightWeight = MathF.Pow(heightWeight, HeightWeightPower);

            return new Vector3
            {
                x = vertex.x + (mainSway * amplitude * heightWeight),
                y = vertex.y + (secondarySway * amplitude * SecondarySwayScale * heightWeight),
                z = vertex.z
            };
        }

        private void InitializeBaseTriangles(I3dObject theObject)
        {
            if (_baseInitialized) return;

            _baseTrianglesByPart.Clear();
            _minTreeZ = float.MaxValue;
            _maxTreeZ = float.MinValue;

            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName != "TreeTrunk" && part.PartName != "TreeFoliage") continue;

                var clone = OmegaObjectHelpers.CopyTriangles(part.Triangles);
                _baseTrianglesByPart[part.PartName!] = clone;

                foreach (var triangle in clone)
                {
                    UpdateZRange((Vector3)triangle.vert1);
                    UpdateZRange((Vector3)triangle.vert2);
                    UpdateZRange((Vector3)triangle.vert3);
                }
            }

            if (_minTreeZ == float.MaxValue || _maxTreeZ == float.MinValue)
            {
                _minTreeZ = 0f;
                _maxTreeZ = 1f;
            }

            _baseInitialized = true;
        }

        private void UpdateZRange(Vector3 vertex)
        {
            if (vertex.z < _minTreeZ) _minTreeZ = vertex.z;
            if (vertex.z > _maxTreeZ) _maxTreeZ = vertex.z;
        }

        private static Vector3 CopyVector(IVector3 vector)
        {
            return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
        }

        public void ReleaseParticles()
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void Dispose()
        {
            _baseTrianglesByPart.Clear();
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
        }

        public void ReleaseParticles(I3dObject theObject)
        {
        }
    }
}
