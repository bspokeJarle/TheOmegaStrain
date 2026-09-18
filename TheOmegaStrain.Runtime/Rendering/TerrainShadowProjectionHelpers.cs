using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Runtime.Rendering;

internal static class TerrainShadowProjectionHelpers
{
    /// <summary>
    /// Drapes freshly generated shadow geometry over the already rotated Surface.
    /// X/Z stay fixed; only Y changes. Call after scaling, before perspective.
    /// Never pass shared model parts or authoritative object geometry here.
    /// </summary>
    internal static void ConformToSurface(
        List<ITriangleMeshWithColorAndTexture> shadowTriangles,
        IReadOnlyList<ITriangleMeshWithColorAndTexture> surfaceTriangles,
        float surfaceLift)
    {
        // A silhouette's triangles share corners. Sample each X/Z once, including
        // misses, and reuse one tile buffer for the engine's height interpolation.
        var heights = new Dictionary<(float X, float Z), float?>();
        var containingTile = new ITriangleMeshWithColorAndTexture[1];
        int retained = 0;
        for (int i = 0; i < shadowTriangles.Count; i++)
        {
            var triangle = shadowTriangles[i];
            var y1 = SampleHeight(triangle.vert1);
            var y2 = SampleHeight(triangle.vert2);
            var y3 = SampleHeight(triangle.vert3);
            // No nearest-tile fallback at viewport edges. Omit incomplete faces
            // rather than stretching them onto unrelated terrain. No clipping yet.
            if (!y1.HasValue || !y2.HasValue || !y3.HasValue)
                continue;

            triangle.vert1 = PlaceVertex(triangle.vert1, y1.Value);
            triangle.vert2 = PlaceVertex(triangle.vert2, y2.Value);
            triangle.vert3 = PlaceVertex(triangle.vert3, y3.Value);
            shadowTriangles[retained++] = triangle;
        }
        shadowTriangles.RemoveRange(retained, shadowTriangles.Count - retained);

        Vector3 PlaceVertex(IVector3 vertex, float groundY) =>
            new(vertex.x, groundY - surfaceLift, vertex.z); // Smaller Y is up.

        float? SampleHeight(IVector3 vertex)
        {
            var key = (vertex.x, vertex.z);
            if (heights.TryGetValue(key, out var cached))
                return cached;

            float? height = TryGetHeight(surfaceTriangles, vertex, containingTile, out float y) ? y : null;
            heights[key] = height;
            return height;
        }
    }

    // Shared strict sampling: neither terrain draping nor edge assistance may
    // use the engine sampler's nearest-tile fallback outside the actual surface.
    internal static bool TryGetHeight(
        IReadOnlyList<ITriangleMeshWithColorAndTexture> surfaceTriangles,
        IVector3 point,
        ITriangleMeshWithColorAndTexture[] containingTile,
        out float height)
    {
        height = 0f;
        if (!float.IsFinite(point.x) || !float.IsFinite(point.z))
            return false;
        foreach (var tile in surfaceTriangles)
        {
            if (!ContainsXZ(tile, point))
                continue;
            containingTile[0] = tile;
            return SurfaceGroundProjectionHelpers.TryGetSurfaceGroundPoint(
                containingTile, point.x, point.z, out _, out height, out _) && float.IsFinite(height);
        }
        return false;
    }

    private static bool ContainsXZ(ITriangleMeshWithColorAndTexture tile, IVector3 point)
    {
        // The engine sampler also succeeds OUTSIDE its mesh (nearest centre).
        // Its containment check is private, so guard with edge signs here and
        // reuse the engine for interpolation rather than duplicating height math.
        float area = Edge(tile.vert1, tile.vert2, tile.vert3);
        if (!float.IsFinite(area) || MathF.Abs(area) < 0.0001f)
            return false;

        float a = Edge(tile.vert1, tile.vert2, point);
        float b = Edge(tile.vert2, tile.vert3, point);
        float c = Edge(tile.vert3, tile.vert1, point);
        const float tolerance = 0.0001f;
        return area > 0f
            ? a >= -tolerance && b >= -tolerance && c >= -tolerance
            : a <= tolerance && b <= tolerance && c <= tolerance;
    }

    private static float Edge(IVector3 a, IVector3 b, IVector3 point) =>
        (b.x - a.x) * (point.z - a.z) - (b.z - a.z) * (point.x - a.x);
}
