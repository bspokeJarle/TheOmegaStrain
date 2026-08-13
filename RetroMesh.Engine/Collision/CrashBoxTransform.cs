using System.Collections;
using System.Collections.Generic;

namespace Domain
{
    public static class CrashBoxTransform
    {
        public static TVector GetEffectiveCrashOffset<TVector>(
            IRenderable3dObject? obj,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            var offset = obj?.CalculatedCrashOffset ?? obj?.ObjectOffsets;
            return offset == null
                ? vectorFactory(0f, 0f, 0f)
                : vectorFactory(offset.x, offset.y, offset.z);
        }

        public static TVector ToLocalPoint<TVector>(
            IVector3 worldPoint,
            IRenderable3dObject? obj,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            var offset = GetEffectiveCrashOffset(obj, vectorFactory);
            return vectorFactory(
                worldPoint.x - offset.x,
                worldPoint.y - offset.y,
                worldPoint.z - offset.z);
        }

        public static TVector ToWorldPoint<TVector>(
            IVector3 localPoint,
            IRenderable3dObject? obj,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            var offset = GetEffectiveCrashOffset(obj, vectorFactory);
            return vectorFactory(
                localPoint.x + offset.x,
                localPoint.y + offset.y,
                localPoint.z + offset.z);
        }

        public static List<TVector> ToCrashWorldPoints<TVector>(
            IEnumerable? localPoints,
            IVector3 offset,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            if (localPoints == null)
                return new List<TVector>();

            int capacity = localPoints is ICollection collection ? collection.Count : 0;
            var result = capacity > 0 ? new List<TVector>(capacity) : new List<TVector>();

            foreach (var item in localPoints)
            {
                if (item is IVector3 point)
                {
                    result.Add(vectorFactory(
                        point.x + offset.x,
                        point.y + offset.y,
                        point.z + offset.z));
                }
            }

            return result;
        }

        public static List<TVector> GetAllCrashPointsWorld<TVector>(
            IRenderable3dObject? obj,
            IVector3 offset,
            Func<float, float, float, TVector> vectorFactory)
            where TVector : IVector3
        {
            if (obj?.CrashBoxes == null || obj.CrashBoxes.Count == 0)
                return new List<TVector>();

            int total = 0;
            for (int i = 0; i < obj.CrashBoxes.Count; i++)
            {
                total += obj.CrashBoxes[i]?.Count ?? 0;
            }

            var result = new List<TVector>(total);
            for (int boxIndex = 0; boxIndex < obj.CrashBoxes.Count; boxIndex++)
            {
                var box = obj.CrashBoxes[boxIndex];
                if (box == null)
                    continue;

                var worldPoints = ToCrashWorldPoints(box, offset, vectorFactory);
                for (int pointIndex = 0; pointIndex < worldPoints.Count; pointIndex++)
                {
                    result.Add(worldPoints[pointIndex]);
                }
            }

            return result;
        }
    }
}
