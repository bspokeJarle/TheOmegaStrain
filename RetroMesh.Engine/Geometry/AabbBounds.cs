using System;
using System.Collections.Generic;

namespace Domain
{
    public readonly record struct AabbBounds(
        float MinX,
        float MaxX,
        float MinY,
        float MaxY,
        float MinZ,
        float MaxZ)
    {
        public static AabbBounds FromPoints(IReadOnlyList<IVector3> points)
        {
            if (points == null || points.Count == 0)
                return new AabbBounds();

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                minX = Math.Min(minX, p.x);
                maxX = Math.Max(maxX, p.x);
                minY = Math.Min(minY, p.y);
                maxY = Math.Max(maxY, p.y);
                minZ = Math.Min(minZ, p.z);
                maxZ = Math.Max(maxZ, p.z);
            }

            return new AabbBounds(minX, maxX, minY, maxY, minZ, maxZ);
        }
    }
}
