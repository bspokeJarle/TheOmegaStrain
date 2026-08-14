using System;

namespace RetroMesh.Engine
{
    public static class CollisionDirectionMath
    {
        public static ImpactDirection EstimateDirection(IVector3 from, IVector3 to)
        {
            float dx = from.x - to.x;
            float dy = from.y - to.y;
            float dz = from.z - to.z;

            return EstimateDominantAxisDirection(dx, dy, dz);
        }

        public static ImpactDirection EstimateDirectionFromAabb(IVector3 point, IVector3 min, IVector3 max)
        {
            float centerX = (min.x + max.x) / 2f;
            float centerY = (min.y + max.y) / 2f;
            float centerZ = (min.z + max.z) / 2f;

            return EstimateDominantAxisDirection(
                point.x - centerX,
                point.y - centerY,
                point.z - centerZ);
        }

        public static ImpactDirection EstimateDirectionFromVisibleMovement(
            IVector3? velocity,
            ImpactDirection fallback)
        {
            if (velocity == null)
                return fallback;

            // Particle physics renders position movement as position -= velocity.
            // Use visible movement direction instead of the already-penetrated AABB center.
            float moveX = -velocity.x;
            float moveY = -velocity.y;
            float moveZ = -velocity.z;
            float absX = Math.Abs(moveX);
            float absY = Math.Abs(moveY);
            float absZ = Math.Abs(moveZ);

            if (absX < 0.001f && absY < 0.001f && absZ < 0.001f)
                return fallback;

            if (absY >= absX && absY >= absZ)
                return moveY >= 0f ? ImpactDirection.Top : ImpactDirection.Bottom;

            if (absX >= absZ)
                return moveX >= 0f ? ImpactDirection.Left : ImpactDirection.Right;

            return fallback;
        }

        private static ImpactDirection EstimateDominantAxisDirection(float dx, float dy, float dz)
        {
            if (Math.Abs(dy) > Math.Abs(dx) && Math.Abs(dy) > Math.Abs(dz))
                return dy < 0 ? ImpactDirection.Top : ImpactDirection.Bottom;

            if (Math.Abs(dx) > Math.Abs(dz))
                return dx > 0 ? ImpactDirection.Right : ImpactDirection.Left;

            return ImpactDirection.Center;
        }
    }
}
