using System;

namespace Domain
{
    public static class RenderShadeMath
    {
        public static float GetDepthFactor01(float calculatedZ, float nearZ, float farZ)
        {
            if (calculatedZ <= nearZ)
                return 0f;

            if (calculatedZ >= farZ)
                return 1f;

            float range = farZ - nearZ;
            if (range <= 0f)
                return 1f;

            return (calculatedZ - nearZ) / range;
        }

        public static float NormalizeAngleTo01(float angle)
        {
            float normalized = (angle + 1f) * 0.5f;
            return Math.Clamp(normalized, 0f, 1f);
        }

        public static float GetTriangleShadeKey(
            float calculatedZ,
            float triangleAngle,
            float nearZ,
            float farZ,
            bool useDepthOnlyShading = false)
        {
            float depthFactor01 = GetDepthFactor01(calculatedZ, nearZ, farZ);
            float combinedFactor01 = useDepthOnlyShading
                ? depthFactor01
                : Math.Clamp(NormalizeAngleTo01(triangleAngle) * depthFactor01, 0f, 1f);

            return (float)Math.Round(combinedFactor01, 2, MidpointRounding.AwayFromZero);
        }
    }
}
