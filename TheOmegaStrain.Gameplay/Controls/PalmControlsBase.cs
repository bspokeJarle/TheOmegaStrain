using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Gameplay.Controls
{
    public abstract class PalmControlsBase : IObjectMovement
    {
        private const float BaseXRotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private const float BaseYRotation = 0f;
        private const float BaseZRotation = 0f;
        private const float SecondarySwayScale = 0.48f;
        private const float TipWeightPower = 1.12f;
        private const float LeafRootRadius = 2.2f;

        private readonly Dictionary<string, List<ITriangleMeshWithColor>> _baseTrianglesByPart = new();
        private DateTime _lastFrameTime = DateTime.MinValue;
        private float _timeSeconds;
        private float _maxLeafRadius = 1f;
        private bool _baseInitialized;

        protected virtual float PhaseOffsetRadians => 0f;
        protected virtual float WindRadiansPerSecond => 1.9f;
        protected virtual float LeafSwayAmplitude => 4.4f;
        protected virtual float LeafFlutterAmplitude => 1.6f;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            theObject.Rotation ??= new Vector3();
            theObject.Rotation.x = BaseXRotation;
            theObject.Rotation.y = BaseYRotation;
            theObject.Rotation.z = BaseZRotation;

            if (!theObject.IsOnScreen)
                return theObject;

            float deltaSeconds = GetDeltaSeconds();
            _timeSeconds += deltaSeconds;

            InitializeBaseTriangles(theObject);
            AnimateLeaves(theObject);

            return theObject;
        }

        private void AnimateLeaves(I3dObject theObject)
        {
            if (_baseTrianglesByPart.Count == 0)
                return;

            float objectPhase = PhaseOffsetRadians + ((theObject.ObjectId % 97) * 0.071f);

            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName == null)
                    continue;

                if (!_baseTrianglesByPart.TryGetValue(part.PartName, out var baseTriangles))
                    continue;

                part.Triangles = CreateWindTriangles(baseTriangles, objectPhase);
            }
        }

        private List<ITriangleMeshWithColor> CreateWindTriangles(
            List<ITriangleMeshWithColor> baseTriangles,
            float phase)
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
                    vert1 = AnimateVertex((Vector3)triangle.vert1, phase),
                    vert2 = AnimateVertex((Vector3)triangle.vert2, phase),
                    vert3 = AnimateVertex((Vector3)triangle.vert3, phase),
                    normal1 = CopyVector(triangle.normal1),
                    normal2 = CopyVector(triangle.normal2),
                    normal3 = CopyVector(triangle.normal3)
                });
            }

            return animated;
        }

        private Vector3 AnimateVertex(Vector3 vertex, float phase)
        {
            float radius = MathF.Sqrt(vertex.x * vertex.x + vertex.y * vertex.y);
            float radiusWeight = Math.Clamp((radius - LeafRootRadius) / Math.Max(1f, _maxLeafRadius - LeafRootRadius), 0f, 1f);
            radiusWeight = MathF.Pow(radiusWeight, TipWeightPower);

            float localPhase = (MathF.Atan2(vertex.y, vertex.x) * 0.22f) + (vertex.z * 0.015f);
            float mainSway = MathF.Sin((_timeSeconds * WindRadiansPerSecond) + phase + localPhase);
            float secondarySway = MathF.Sin((_timeSeconds * WindRadiansPerSecond * 0.73f) + phase + localPhase + 0.7f);
            float flutter = MathF.Sin((_timeSeconds * WindRadiansPerSecond * 1.45f) + phase + localPhase + (radius * 0.04f));

            return new Vector3
            {
                x = vertex.x + (mainSway * LeafSwayAmplitude * radiusWeight),
                y = vertex.y + (secondarySway * LeafSwayAmplitude * SecondarySwayScale * radiusWeight),
                z = vertex.z + (flutter * LeafFlutterAmplitude * radiusWeight)
            };
        }

        private void InitializeBaseTriangles(I3dObject theObject)
        {
            if (_baseInitialized)
                return;

            _baseTrianglesByPart.Clear();
            _maxLeafRadius = 1f;

            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName == null || !part.PartName.EndsWith("PalmLeaves", StringComparison.Ordinal))
                    continue;

                var clone = OmegaObjectHelpers.CopyTriangles(part.Triangles);
                _baseTrianglesByPart[part.PartName] = clone;

                foreach (var triangle in clone)
                {
                    UpdateMaxRadius((Vector3)triangle.vert1);
                    UpdateMaxRadius((Vector3)triangle.vert2);
                    UpdateMaxRadius((Vector3)triangle.vert3);
                }
            }

            _baseInitialized = true;
        }

        private void UpdateMaxRadius(Vector3 vertex)
        {
            float radius = MathF.Sqrt(vertex.x * vertex.x + vertex.y * vertex.y);
            if (radius > _maxLeafRadius)
                _maxLeafRadius = radius;
        }

        private static Vector3 CopyVector(IVector3 vector)
        {
            return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
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

        public void ReleaseParticles()
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
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

        public void Dispose()
        {
            _baseTrianglesByPart.Clear();
            _lastFrameTime = DateTime.MinValue;
            _timeSeconds = 0f;
            _baseInitialized = false;
            _maxLeafRadius = 1f;
            StartCoordinates = null;
            GuideCoordinates = null;
        }
    }
}
