using TheOmegaStrain.Domain;
using System.Collections;

namespace TheOmegaStrain.Common.OmegaEngineAdapters
{
    public static class OmegaCrashBoxOffsetExtensions
    {
        public static Vector3 ToLocalPoint(this Vector3 worldPoint, OmegaObject3D obj)
        {
            if (obj == null)
                return worldPoint;

            return CrashBoxTransform.ToLocalPoint(
                worldPoint,
                obj,
                static (x, y, z) => new Vector3(x, y, z));
        }

        public static Vector3 ToWorldPoint(this Vector3 localPoint, OmegaObject3D obj)
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
        public static Vector3 GetEffectiveCrashOffset(this OmegaObject3D obj)
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

        public static List<Vector3> GetAllCrashPointsWorld(this OmegaObject3D obj)
        {
            if (obj == null)
                return new List<Vector3>();

            return obj.GetAllCrashPointsWorld(obj.GetEffectiveCrashOffset());
        }

        public static List<Vector3> GetAllCrashPointsWorld(this OmegaObject3D obj, Vector3 offset)
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
