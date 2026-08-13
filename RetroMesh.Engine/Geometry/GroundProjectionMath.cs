using System;
using System.Collections.Generic;

namespace Domain
{
    public static class GroundProjectionMath
    {
        public const float DefaultShadowStaticOffsetX = -30f;
        public const float DefaultShadowStaticOffsetY = -40f;
        public const float DefaultShadowStaticOffsetZ = 0f;
        public const float DefaultShadowBaseScale = 1.0f;
        public const float DefaultShadowMinScale = 0.2f;
        public const float DefaultShadowAltitudeShrinkFactor = 0.002f;
        public const float DefaultShadowSlopeX = -0.15f;
        public const float DefaultShadowSlopeY = -0.55f;
        public const float DefaultShadowVertexStretchBoost = 1.2f;

        public static bool TryGetSurfaceGroundPoint(
            IReadOnlyList<ITriangleMeshWithColor>? rotatedTiles,
            float targetX,
            float targetZ,
            out float groundX,
            out float groundY,
            out float groundZ)
        {
            groundX = targetX;
            groundY = 0f;
            groundZ = targetZ;

            if (rotatedTiles == null || rotatedTiles.Count == 0)
                return false;

            float fallbackY = 0f;
            float bestDistSq = float.MaxValue;
            bool hasFallback = false;

            for (int i = 0; i < rotatedTiles.Count; i++)
            {
                var tile = rotatedTiles[i];
                if (TryInterpolateTriangleY(tile, targetX, targetZ, out float interpolatedY))
                {
                    groundY = interpolatedY;
                    return true;
                }

                float tileCenterX = (tile.vert1.x + tile.vert2.x + tile.vert3.x) / 3f;
                float tileCenterZ = (tile.vert1.z + tile.vert2.z + tile.vert3.z) / 3f;
                float dx = tileCenterX - targetX;
                float dz = tileCenterZ - targetZ;
                float distanceSquared = dx * dx + dz * dz;
                if (distanceSquared < bestDistSq)
                {
                    bestDistSq = distanceSquared;
                    fallbackY = (tile.vert1.y + tile.vert2.y + tile.vert3.y) / 3f;
                    hasFallback = true;
                }
            }

            if (!hasFallback)
                return false;

            groundY = fallbackY;
            return true;
        }

        public static bool TryGetFrontmostSurfaceGroundPoint(
            IReadOnlyList<ITriangleMeshWithColor>? rotatedTiles,
            float targetX,
            out float groundX,
            out float groundY,
            out float groundZ)
        {
            groundX = targetX;
            groundY = 0f;
            groundZ = 0f;

            if (rotatedTiles == null || rotatedTiles.Count == 0)
                return false;

            float minAbsY = float.MaxValue;
            bool found = false;

            for (int i = 0; i < rotatedTiles.Count; i++)
            {
                var tile = rotatedTiles[i];
                float tileCenterY = (tile.vert1.y + tile.vert2.y + tile.vert3.y) / 3f;
                float absY = MathF.Abs(tileCenterY);
                if (absY >= minAbsY)
                    continue;

                minAbsY = absY;
                groundY = tileCenterY;
                groundZ = (tile.vert1.z + tile.vert2.z + tile.vert3.z) / 3f;
                found = true;
            }

            return found;
        }

        private static bool TryInterpolateTriangleY(
            ITriangleMeshWithColor triangle,
            float targetX,
            float targetZ,
            out float groundY)
        {
            groundY = 0f;
            const float epsilon = 0.001f;

            float x1 = triangle.vert1.x;
            float z1 = triangle.vert1.z;
            float x2 = triangle.vert2.x;
            float z2 = triangle.vert2.z;
            float x3 = triangle.vert3.x;
            float z3 = triangle.vert3.z;

            float denominator = ((z2 - z3) * (x1 - x3)) + ((x3 - x2) * (z1 - z3));
            if (MathF.Abs(denominator) < 0.0001f)
                return false;

            float weight1 = (((z2 - z3) * (targetX - x3)) + ((x3 - x2) * (targetZ - z3))) / denominator;
            float weight2 = (((z3 - z1) * (targetX - x3)) + ((x1 - x3) * (targetZ - z3))) / denominator;
            float weight3 = 1f - weight1 - weight2;

            if (weight1 < -epsilon || weight2 < -epsilon || weight3 < -epsilon)
                return false;

            groundY = (weight1 * triangle.vert1.y)
                + (weight2 * triangle.vert2.y)
                + (weight3 * triangle.vert3.y);
            return true;
        }
    }
}
