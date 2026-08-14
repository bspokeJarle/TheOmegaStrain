using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public static class EngineObjectCloner
    {
        public static TObject CopyRenderableObject<TObject>(
            IRenderable3dObject original,
            Func<int, TObject> objectFactory,
            Func<I3dObjectPart> objectPartFactory,
            Func<ITriangleMeshWithColor> triangleFactory,
            Func<IVector3, IVector3> vectorFactory,
            bool copyCrashboxes)
            where TObject : IRenderable3dObject
        {
            var copy = objectFactory(original.ObjectId);

            copy.ObjectId = original.ObjectId;
            copy.ObjectOffsets = CopyOptionalVector(original.ObjectOffsets, vectorFactory);
            copy.Rotation = CopyOptionalVector(original.Rotation, vectorFactory);
            copy.WorldPosition = CopyOptionalVector(original.WorldPosition, vectorFactory);
            copy.ObjectParts = CopyObjectParts(original.ObjectParts, objectPartFactory, triangleFactory, vectorFactory);
            copy.ObjectName = original.ObjectName;
            copy.RotationOffsetX = original.RotationOffsetX;
            copy.RotationOffsetY = original.RotationOffsetY;
            copy.RotationOffsetZ = original.RotationOffsetZ;
            copy.CrashBoxes = copyCrashboxes
                ? GeometryMath.CopyCrashboxes(original.CrashBoxes, vectorFactory)
                : original.CrashBoxes;
            copy.CrashBoxNames = original.CrashBoxNames;
            copy.CrashBoxesFollowRotation = original.CrashBoxesFollowRotation;
            copy.CalculatedCrashOffset = CopyOptionalVector(original.CalculatedCrashOffset, vectorFactory);
            copy.IsOnScreen = original.IsOnScreen;
            copy.HasShadow = original.HasShadow;
            copy.ShadowOffset = CopyOptionalVector(original.ShadowOffset, vectorFactory);
            copy.UseSurfaceFootprintPivot = original.UseSurfaceFootprintPivot;
            copy.IsActive = original.IsActive;
            copy.ZSortBias = original.ZSortBias;

            return copy;
        }

        public static List<I3dObjectPart> CopyObjectParts(
            IReadOnlyList<I3dObjectPart> originalParts,
            Func<I3dObjectPart> objectPartFactory,
            Func<ITriangleMeshWithColor> triangleFactory,
            Func<IVector3, IVector3> vectorFactory)
        {
            var objectParts = new List<I3dObjectPart>(originalParts.Count);

            for (int partIndex = 0; partIndex < originalParts.Count; partIndex++)
            {
                var part = originalParts[partIndex];
                var objectPart = objectPartFactory();
                objectPart.PartName = part.PartName;
                objectPart.Triangles = CopyTriangles(part.Triangles, triangleFactory, vectorFactory);
                objectPart.IsVisible = part.IsVisible;
                objectParts.Add(objectPart);
            }

            return objectParts;
        }

        public static List<ITriangleMeshWithColor> CopyTriangles(
            IReadOnlyList<ITriangleMeshWithColor> triangles,
            Func<ITriangleMeshWithColor> triangleFactory,
            Func<IVector3, IVector3> vectorFactory)
        {
            var copiedTriangles = new List<ITriangleMeshWithColor>(triangles.Count);

            for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++)
            {
                copiedTriangles.Add(CopyTriangle(triangles[triangleIndex], triangleFactory, vectorFactory));
            }

            return copiedTriangles;
        }

        public static ITriangleMeshWithColor CopyTriangle(
            ITriangleMeshWithColor triangle,
            Func<ITriangleMeshWithColor> triangleFactory,
            Func<IVector3, IVector3> vectorFactory)
        {
            var triangleCopy = triangleFactory();
            triangleCopy.landBasedPosition = triangle.landBasedPosition;
            triangleCopy.angle = triangle.angle;
            triangleCopy.Color = triangle.Color;
            triangleCopy.noHidden = triangle.noHidden;

            var mesh = triangle as EngineTriangleMesh;
            CopyOptionalVector(mesh != null ? mesh.Vert1Raw : triangle.vert1, vectorFactory, value => triangleCopy.vert1 = value);
            CopyOptionalVector(mesh != null ? mesh.Vert2Raw : triangle.vert2, vectorFactory, value => triangleCopy.vert2 = value);
            CopyOptionalVector(mesh != null ? mesh.Vert3Raw : triangle.vert3, vectorFactory, value => triangleCopy.vert3 = value);
            CopyOptionalVector(mesh != null ? mesh.Normal1Raw : triangle.normal1, vectorFactory, value => triangleCopy.normal1 = value);
            CopyOptionalVector(mesh != null ? mesh.Normal2Raw : triangle.normal2, vectorFactory, value => triangleCopy.normal2 = value);
            CopyOptionalVector(mesh != null ? mesh.Normal3Raw : triangle.normal3, vectorFactory, value => triangleCopy.normal3 = value);

            return triangleCopy;
        }

        private static IVector3? CopyOptionalVector(IVector3? vector, Func<IVector3, IVector3> vectorFactory)
        {
            return vector == null ? null : vectorFactory(vector);
        }

        private static void CopyOptionalVector(
            IVector3? vector,
            Func<IVector3, IVector3> vectorFactory,
            Action<IVector3> assign)
        {
            if (vector == null)
                return;

            assign(vectorFactory(vector));
        }
    }
}
