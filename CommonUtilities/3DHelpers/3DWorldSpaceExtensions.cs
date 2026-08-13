using Domain;
using System.Collections;
using static Domain._3dSpecificsImplementations;

namespace CommonUtilities._3DHelpers
{
    public static class CrashBoxOffsetExtensions
    {
        public static Vector3 ToLocalPoint(this Vector3 worldPoint, _3dObject obj)
        {
            if (obj == null)
                return worldPoint;

            return CrashBoxTransform.ToLocalPoint(
                worldPoint,
                obj,
                static (x, y, z) => new Vector3(x, y, z));
        }

        public static Vector3 ToWorldPoint(this Vector3 localPoint, _3dObject obj)
        {
            if (obj == null)
                return localPoint;

            return CrashBoxTransform.ToWorldPoint(
                localPoint,
                obj,
                static (x, y, z) => new Vector3(x, y, z));
        }

        // Keep this returning the SAME Vector3 type that crashboxes are made of.
        // CalculatedCrashOffset already includes ObjectOffsets for world objects.
        // Screen objects (Ship) fall back to ObjectOffsets.
        public static Vector3 GetEffectiveCrashOffset(this _3dObject obj)
        {
            return CrashBoxTransform.GetEffectiveCrashOffset(
                obj,
                static (x, y, z) => new Vector3(x, y, z));
        }

        public static List<Vector3> ToCrashWorldPoints(this IReadOnlyList<Vector3> localPoints, Vector3 offset)
        {
            return CrashBoxTransform.ToCrashWorldPoints(
                localPoints,
                offset,
                static (x, y, z) => new Vector3(x, y, z));
        }

        public static List<Vector3> GetAllCrashPointsWorld(this _3dObject obj)
        {
            if (obj == null)
                return new List<Vector3>();

            return obj.GetAllCrashPointsWorld(obj.GetEffectiveCrashOffset());
        }

        public static List<Vector3> GetAllCrashPointsWorld(this _3dObject obj, Vector3 offset)
        {
            return CrashBoxTransform.GetAllCrashPointsWorld(
                obj,
                offset,
                static (x, y, z) => new Vector3(x, y, z));
        }

        public static List<Vector3> ToCrashWorldPoints(this IEnumerable localPoints, Vector3 offset)
        {
            return CrashBoxTransform.ToCrashWorldPoints(
                localPoints,
                offset,
                static (x, y, z) => new Vector3(x, y, z));
        }
    }
}
