using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public static class CollisionBoxMath
    {
        public static bool CheckAabbOverlap(
            IReadOnlyList<IVector3> boxA,
            IReadOnlyList<IVector3> boxB,
            float marginX,
            float marginY,
            float marginZ,
            out AabbBounds boundsA,
            out AabbBounds boundsB)
        {
            return MeshGeometryOperations.CheckAabbOverlap(
                boxA,
                boxB,
                marginX,
                marginY,
                marginZ,
                out boundsA,
                out boundsB);
        }

        public static bool ContainsPoint(
            IReadOnlyList<IVector3>? box,
            IVector3 point,
            out AabbBounds bounds)
        {
            bounds = box == null || box.Count == 0
                ? new AabbBounds()
                : AabbBounds.FromPoints(box);

            if (box == null || box.Count == 0)
                return false;

            return point.x >= bounds.MinX && point.x <= bounds.MaxX &&
                   point.y >= bounds.MinY && point.y <= bounds.MaxY &&
                   point.z >= bounds.MinZ && point.z <= bounds.MaxZ;
        }

        public static EngineVector3 GetCenter(AabbBounds bounds)
        {
            return new EngineVector3(
                (bounds.MinX + bounds.MaxX) / 2f,
                (bounds.MinY + bounds.MaxY) / 2f,
                (bounds.MinZ + bounds.MaxZ) / 2f);
        }

        public static bool RangesOverlap(float minA, float maxA, float minB, float maxB, float margin)
        {
            return (maxA + margin) >= (minB - margin) &&
                   (minA - margin) <= (maxB + margin);
        }
    }
}
