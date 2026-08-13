using Domain;
using System.Collections;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace CommonUtilities._3DHelpers
{
    public static class CrashBoxOffsetExtensions
    {
        public static Vector3 ToLocalPoint(this Vector3 worldPoint, _3dObject obj)
        {
            if (obj == null)
                return worldPoint;

            var offset = obj.GetEffectiveCrashOffset();
             
             return new Vector3
            {
                 x = worldPoint.x - offset.x,
                 y = worldPoint.y - offset.y,
                z = worldPoint.z - offset.z
            };  
        }   

        public static Vector3 ToWorldPoint(this Vector3 localPoint, _3dObject obj)
        {  
            if (obj == null)
                return localPoint;

            var offset = obj.GetEffectiveCrashOffset();

            return new Vector3
            {
                x = localPoint.x + offset.x,
                y = localPoint.y + offset.y,
                z = localPoint.z + offset.z
            };
        }
        // Keep this returning the SAME Vector3 type that crashboxes are made of.
        // This is the effective local offset used to keep crash boxes aligned with rendering.
        // CalculatedCrashOffset (set in TryGetRenderPosition) already includes ObjectOffsets,
        // so we use it directly for world objects. Screen objects (Ship) fall back to ObjectOffsets.
        public static Vector3 GetEffectiveCrashOffset(this _3dObject obj)
        {
            var local = obj?.ObjectOffsets ?? new Vector3(0, 0, 0);
            var crashOffset = obj?.CalculatedCrashOffset;

            if (crashOffset != null)
            {
                return new Vector3(
                    crashOffset.x,
                    crashOffset.y,
                    crashOffset.z
                );
            }

            return new Vector3(
                local.x,
                local.y,
                local.z
            );
        }

        // Preferred overload (fast, type-safe) when the box is strongly typed.
        public static List<Vector3> ToCrashWorldPoints(this IReadOnlyList<Vector3> localPoints, Vector3 offset)
        {
            if (localPoints == null || localPoints.Count == 0)
                return new List<Vector3>();

            var result = new List<Vector3>(localPoints.Count);
            for (int i = 0; i < localPoints.Count; i++)
            {
                var p = localPoints[i];
                result.Add(new Vector3 { x = p.x + offset.x, y = p.y + offset.y, z = p.z + offset.z });
            }
            return result;
        }

        public static List<Vector3> GetAllCrashPointsWorld(this _3dObject obj)
        {
            if (obj == null) return new List<Vector3>();
            return obj.GetAllCrashPointsWorld(obj.GetEffectiveCrashOffset());
        }

        public static List<Vector3> GetAllCrashPointsWorld(this _3dObject obj, Vector3 offset)
        {
            if (obj?.CrashBoxes == null || obj.CrashBoxes.Count == 0)
                return new List<Vector3>();

            // Estimate capacity to reduce reallocations
            int total = 0;
            for (int i = 0; i < obj.CrashBoxes.Count; i++)
            {
                var box = obj.CrashBoxes[i];
                if (box is System.Collections.ICollection col)
                    total += col.Count;
                else
                    total += 8; // fallback guess for typical 8-corner boxes
            }

            var result = new List<Vector3>(total);

            for (int b = 0; b < obj.CrashBoxes.Count; b++)
            {
                var box = obj.CrashBoxes[b];
                if (box == null) continue;

                // Use the bridge overload (IEnumerable) so we don't care about exact list type
                var worldPoints = ((System.Collections.IEnumerable)box).ToCrashWorldPoints(offset);

                for (int p = 0; p < worldPoints.Count; p++)
                    result.Add(worldPoints[p]);
            }

            return result;
        }


        // Bridge overload: works when crashboxes are stored as non-generic lists (IList / IEnumerable / object list).
        // Avoids needing .Cast<Vector3>().ToList() everywhere.
        public static List<Vector3> ToCrashWorldPoints(this IEnumerable localPoints, Vector3 offset)
        {
            if (localPoints == null)
                return new List<Vector3>();

            // Try to get a reasonable capacity when possible
            int capacity = 0;
            if (localPoints is ICollection col) capacity = col.Count;

            var result = capacity > 0 ? new List<Vector3>(capacity) : new List<Vector3>();

            foreach (var item in localPoints)
            {
                // Only accept the correct Vector3 type (Domain._3dSpecificsImplementations.Vector3)
                if (item is Vector3 p)
                {
                    result.Add(new Vector3
                    {
                        x = p.x + offset.x,
                        y = p.y + offset.y,
                        z = p.z + offset.z
                    });
                }
            }

            return result;
        }
    }
}
