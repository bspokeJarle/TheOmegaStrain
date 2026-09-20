using RetroMesh.Engine;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Common.OmegaEngineAdapters;

/// <summary>
/// Converts object render anchors into the rotated Surface vertex space used
/// by terrain sampling. This keeps shadows and flying-object clearance in the
/// same coordinate system.
/// </summary>
public static class SurfaceRenderAnchorHelpers
{
    public const double MaximumPerspectiveScale = 2.5d;

    public static bool TryGetSurfaceLocalAnchor(
        RenderPosition objectPosition,
        RenderPosition surfacePosition,
        out float localX,
        out float localY,
        out float localZ)
    {
        return TryGetSurfaceLocalAnchor(
            objectPosition, surfacePosition,
            out localX, out localY, out localZ, out _);
    }

    public static bool TryGetSurfaceLocalAnchor(
        RenderPosition objectPosition,
        RenderPosition surfacePosition,
        out float localX,
        out float localY,
        out float localZ,
        out float objectProjectionScale)
    {
        localX = localY = localZ = 0f;
        objectProjectionScale = 0f;
        double perspective = ScreenSetup.perspectiveAdjustment;
        double objectDepth = ClampRenderDepth(objectPosition.Z, perspective);
        double surfaceDepth = ClampRenderDepth(surfacePosition.Z, perspective);

        if (!ProjectionMath.TryProjectVertex(new Vector3(1f, 0f, 0f),
                0, 0, objectDepth, perspective, ScreenSetup.defaultObjectZoom, out var unit)
            || !double.IsFinite(unit.x) || unit.x <= 0d)
            return false;

        localX = (float)((objectPosition.X - surfacePosition.X) / unit.x);
        localY = (float)((objectPosition.Y - surfacePosition.Y) / unit.x);
        localZ = (float)(surfaceDepth - objectDepth);
        objectProjectionScale = (float)unit.x;
        return float.IsFinite(localX) && float.IsFinite(localY) && float.IsFinite(localZ)
            && float.IsFinite(objectProjectionScale) && objectProjectionScale > 0f;
    }

    public static double ClampRenderDepth(double screenZ, double perspectiveAdjustment)
    {
        if (perspectiveAdjustment <= 0d)
            return screenZ;

        double nearestRenderDepth =
            (perspectiveAdjustment / MaximumPerspectiveScale) - perspectiveAdjustment;
        return Math.Max(screenZ, nearestRenderDepth);
    }
}
