using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class TowerControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        private float Yrotation = 0;
        private float Xrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private float Zrotation = 0;

        private float TowerRotationSpeedZ = 1.5f;
        private float TowerZRotation = 0;
        private bool _baseOffsetInitialized;
        private float _baseOffsetY;

        private readonly OmegaMeshRotation _rotate = new(); // Rotation fra OmegaEngineAdapters
        private readonly Dictionary<string, List<ITriangleMeshWithColor>> _originalTopPartMeshes = new();

        // Shared XY pivots for the rotating head clusters. The whole tower is shifted by
        // NormalizeSurfaceFootprintPivot during construction (the bottom-band centroid is
        // pulled off (0,0) by asymmetric parts like the door decals), so the head ring is
        // no longer centered on the world Z axis. Rotating around world origin would make
        // the head orbit instead of spinning in place. All head parts of a given variant
        // must share one pivot so they stay rigidly attached to each other.
        private static readonly string[] TowerHeadPartNames =
        {
            "TowerHeadFrame",
            "TowerHeadGlass",
            "TowerRoof",
            "TowerRadar"
        };

        private static readonly string[] SnowTowerHeadPartNames =
        {
            "SnowTowerHeadFrame",
            "SnowTowerGlass",
            "SnowTowerSnowLid",
            "SnowTowerAntenna"
        };

        private Vector3? _towerHeadPivot;
        private Vector3? _snowTowerHeadPivot;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            //TODO: In time I want the trees to have animations on the branches, now nothing
            //Set parent object
            ParentObject = theObject;
            if (theObject.Rotation != null) theObject.Rotation.y = Yrotation;
            if (theObject.Rotation != null) theObject.Rotation.x = Xrotation;
            if (theObject.Rotation != null) theObject.Rotation.z = Zrotation;

            ApplyGroundContactNudge(theObject);

            //For now, just rotate the object at a fixed speed
            //Zrotation += 2;
            //Xrotation += 2;

            RotateUpperTowerAnimation();

            return theObject;
        }

        private void ApplyGroundContactNudge(I3dObject theObject)
        {
            if (theObject.ObjectOffsets == null)
                return;

            if (!_baseOffsetInitialized)
            {
                _baseOffsetY = theObject.ObjectOffsets.y;
                _baseOffsetInitialized = true;
            }

            theObject.ObjectOffsets = new Vector3
            {
                x = theObject.ObjectOffsets.x,
                y = _baseOffsetY + LandBasedObjectSetup.GroundContactNudgeYScaled,
                z = theObject.ObjectOffsets.z
            };
        }

        public void RotateUpperTowerAnimation()
        {
            TowerZRotation += TowerRotationSpeedZ * GameState.FrameScale90;

            if (ParentObject == null) return;
            CacheOriginalTopParts();

            var towerHeadFrame = ParentObject.ObjectParts.Find(obj => obj.PartName == "TowerHeadFrame");
            var towerHeadGlass = ParentObject.ObjectParts.Find(obj => obj.PartName == "TowerHeadGlass");
            var towerRoof = ParentObject.ObjectParts.Find(obj => obj.PartName == "TowerRoof");
            var towerRadar = ParentObject.ObjectParts.Find(obj => obj.PartName == "TowerRadar");
            var snowTowerHeadFrame = ParentObject.ObjectParts.Find(obj => obj.PartName == "SnowTowerHeadFrame");
            var snowTowerGlass = ParentObject.ObjectParts.Find(obj => obj.PartName == "SnowTowerGlass");
            var snowTowerSnowLid = ParentObject.ObjectParts.Find(obj => obj.PartName == "SnowTowerSnowLid");
            var snowTowerAntenna = ParentObject.ObjectParts.Find(obj => obj.PartName == "SnowTowerAntenna");

            if (towerHeadFrame != null && towerHeadGlass != null && towerRoof != null && towerRadar != null)
            {
                var pivot = ResolveTowerHeadPivot();
                ApplyRotatedTriangles(towerHeadFrame, pivot);
                ApplyRotatedTriangles(towerHeadGlass, pivot);
                ApplyRotatedTriangles(towerRoof, pivot);
                ApplyRotatedTriangles(towerRadar, pivot);
            }

            if (snowTowerHeadFrame != null && snowTowerGlass != null && snowTowerSnowLid != null && snowTowerAntenna != null)
            {
                var pivot = ResolveSnowTowerHeadPivot();
                ApplyRotatedTriangles(snowTowerHeadFrame, pivot);
                ApplyRotatedTriangles(snowTowerGlass, pivot);
                ApplyRotatedTriangles(snowTowerSnowLid, pivot);
                ApplyRotatedTriangles(snowTowerAntenna, pivot);
            }
        }

        private void CacheOriginalTopParts()
        {
            if (ParentObject == null)
                return;

            CacheOriginalPart("TowerHeadFrame");
            CacheOriginalPart("TowerHeadGlass");
            CacheOriginalPart("TowerRoof");
            CacheOriginalPart("TowerRadar");

            CacheOriginalPart("SnowTowerHeadFrame");
            CacheOriginalPart("SnowTowerGlass");
            CacheOriginalPart("SnowTowerSnowLid");
            CacheOriginalPart("SnowTowerAntenna");
        }

        private void CacheOriginalPart(string partName)
        {
            if (_originalTopPartMeshes.ContainsKey(partName))
                return;

            var part = ParentObject.ObjectParts.Find(obj => obj.PartName == partName);
            if (part == null)
                return;

            _originalTopPartMeshes[partName] = OmegaObjectHelpers.CopyTriangles(part.Triangles);
        }

        private void ApplyRotatedTriangles(I3dObjectPart part, Vector3 pivot)
        {
            if (part.PartName == null)
                return;
            if (!_originalTopPartMeshes.TryGetValue(part.PartName, out var baseMesh))
                return;

            var source = OmegaObjectHelpers.CopyTriangles(baseMesh);
            TranslateMeshInPlace(source, -pivot.x, -pivot.y, 0f);
            var rotated = _rotate.RotateZMesh(source, TowerZRotation);
            TranslateMeshInPlace(rotated, pivot.x, pivot.y, 0f);
            part.Triangles = rotated;
        }

        private Vector3 ResolveTowerHeadPivot()
        {
            if (_towerHeadPivot != null)
                return _towerHeadPivot;

            _towerHeadPivot = ComputeSharedXyCenter(TowerHeadPartNames);
            return _towerHeadPivot;
        }

        private Vector3 ResolveSnowTowerHeadPivot()
        {
            if (_snowTowerHeadPivot != null)
                return _snowTowerHeadPivot;

            _snowTowerHeadPivot = ComputeSharedXyCenter(SnowTowerHeadPartNames);
            return _snowTowerHeadPivot;
        }

        // Computes the XY bounding-box center across the cached original meshes of all
        // named parts so a cluster of head parts can share a single pivot. Z is left at 0
        // because we rotate around an axis parallel to world Z.
        private Vector3 ComputeSharedXyCenter(string[] partNames)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool any = false;

            foreach (var name in partNames)
            {
                if (!_originalTopPartMeshes.TryGetValue(name, out var triangles) || triangles == null)
                    continue;

                foreach (var triangle in triangles)
                {
                    AccumulateXyBounds(triangle.vert1, ref minX, ref minY, ref maxX, ref maxY, ref any);
                    AccumulateXyBounds(triangle.vert2, ref minX, ref minY, ref maxX, ref maxY, ref any);
                    AccumulateXyBounds(triangle.vert3, ref minX, ref minY, ref maxX, ref maxY, ref any);
                }
            }

            if (!any)
                return new Vector3();

            return new Vector3
            {
                x = (minX + maxX) * 0.5f,
                y = (minY + maxY) * 0.5f,
                z = 0f
            };
        }

        private static void AccumulateXyBounds(IVector3 vertex, ref float minX, ref float minY, ref float maxX, ref float maxY, ref bool any)
        {
            if (vertex == null)
                return;

            if (vertex.x < minX) minX = vertex.x;
            if (vertex.y < minY) minY = vertex.y;
            if (vertex.x > maxX) maxX = vertex.x;
            if (vertex.y > maxY) maxY = vertex.y;
            any = true;
        }

        private static void TranslateMeshInPlace(List<ITriangleMeshWithColor> mesh, float shiftX, float shiftY, float shiftZ)
        {
            for (int i = 0; i < mesh.Count; i++)
            {
                var triangle = mesh[i];
                TranslateVertexInPlace(triangle.vert1, shiftX, shiftY, shiftZ);
                TranslateVertexInPlace(triangle.vert2, shiftX, shiftY, shiftZ);
                TranslateVertexInPlace(triangle.vert3, shiftX, shiftY, shiftZ);
            }
        }

        private static void TranslateVertexInPlace(IVector3 vertex, float shiftX, float shiftY, float shiftZ)
        {
            if (vertex == null)
                return;

            vertex.x += shiftX;
            vertex.y += shiftY;
            vertex.z += shiftZ;
        }

        public void ReleaseParticles()
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void Dispose()
        {
            _originalTopPartMeshes.Clear();
            ParentObject = null!;
            StartCoordinates = null;
            GuideCoordinates = null;
            TowerZRotation = 0f;
            _baseOffsetInitialized = false;
            _baseOffsetY = 0f;
            _towerHeadPivot = null;
            _snowTowerHeadPivot = null;
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
