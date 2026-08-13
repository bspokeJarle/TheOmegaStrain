using Domain;

namespace CommonUtilities._3DHelpers
{
    public static class SurfaceGroundProjectionHelpers
    {
        public const float DefaultShadowStaticOffsetX = GroundProjectionMath.DefaultShadowStaticOffsetX;
        public const float DefaultShadowStaticOffsetY = GroundProjectionMath.DefaultShadowStaticOffsetY;
        public const float DefaultShadowStaticOffsetZ = GroundProjectionMath.DefaultShadowStaticOffsetZ;
        public const float DefaultShadowBaseScale = GroundProjectionMath.DefaultShadowBaseScale;
        public const float DefaultShadowMinScale = GroundProjectionMath.DefaultShadowMinScale;
        public const float DefaultShadowAltitudeShrinkFactor = GroundProjectionMath.DefaultShadowAltitudeShrinkFactor;
        public const float DefaultShadowSlopeX = GroundProjectionMath.DefaultShadowSlopeX;
        public const float DefaultShadowSlopeY = GroundProjectionMath.DefaultShadowSlopeY;
        public const float DefaultShadowVertexStretchBoost = GroundProjectionMath.DefaultShadowVertexStretchBoost;

        public static bool TryGetSurfaceGroundPoint(
            IReadOnlyList<ITriangleMeshWithColor>? rotatedTiles,
            float targetX,
            float targetZ,
            out float groundX,
            out float groundY,
            out float groundZ)
        {
            return GroundProjectionMath.TryGetSurfaceGroundPoint(
                rotatedTiles,
                targetX,
                targetZ,
                out groundX,
                out groundY,
                out groundZ);
        }

        public static bool TryGetFrontmostSurfaceGroundPoint(
            IReadOnlyList<ITriangleMeshWithColor>? rotatedTiles,
            float targetX,
            out float groundX,
            out float groundY,
            out float groundZ)
        {
            return GroundProjectionMath.TryGetFrontmostSurfaceGroundPoint(
                rotatedTiles,
                targetX,
                out groundX,
                out groundY,
                out groundZ);
        }
    }
}
