using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Projection;

namespace TheOmegaStrain.Runtime.Rendering;

internal static class ShadowViewportHelpers
{
    /// <summary>
    /// Keeps the foreground shadow's centre just inside the bottom of the image,
    /// allowing roughly half its footprint to leave the screen. Move only generated
    /// shadow vertices inward along Surface Z; ConformToSurface must run afterwards.
    /// Nothing is pinned at the horizon or for an off-screen caster.
    /// </summary>
    internal static void KeepForegroundVisible(
        OmegaObject3D caster,
        RenderPosition casterPosition,
        List<ITriangleMeshWithColorAndTexture> shadowTriangles,
        IReadOnlyList<ITriangleMeshWithColorAndTexture> surfaceTriangles,
        RenderPosition surfacePosition,
        IProjectionViewport viewport,
        float surfaceLift)
    {
        if (!caster.IsOnScreen || shadowTriangles.Count == 0 || surfaceTriangles.Count == 0)
            return;

        var vertices = new List<IVector3>(shadowTriangles.Count * 3);
        foreach (var triangle in shadowTriangles)
        {
            vertices.Add(triangle.vert1);
            vertices.Add(triangle.vert2);
            vertices.Add(triangle.vert3);
        }
        var bounds = AabbBounds.FromPoints(vertices);
        float anchorX = (bounds.MinX + bounds.MaxX) / 2f;
        float anchorZ = (bounds.MinZ + bounds.MaxZ) / 2f;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var tile in surfaceTriangles)
        {
            minZ = MathF.Min(minZ, MathF.Min(tile.vert1.z, MathF.Min(tile.vert2.z, tile.vert3.z)));
            maxZ = MathF.Max(maxZ, MathF.Max(tile.vert1.z, MathF.Max(tile.vert2.z, tile.vert3.z)));
        }
        float backLimit = (minZ + maxZ) / 2f;
        if (!float.IsFinite(anchorZ) || anchorZ <= backLimit)
            return; // Only the foreground; never pull a horizon shadow forward.

        // Leave room for the footprint at the physical edge too, before terrain
        // draping discards faces that would otherwise fall outside Surface.
        float frontLimit = maxZ - (bounds.MaxZ - bounds.MinZ) / 2f;
        float edgePadding = viewport.ScreenHeight * (2f / 1024f);
        double bottom = viewport.ScreenHeight - edgePadding;
        var sample = new Vector3(anchorX, 0f, 0f);
        var tileBuffer = new ITriangleMeshWithColorAndTexture[1];
        double surfaceDepth = OmegaPerspectiveProjectorFactory.ClampRenderDepth(surfacePosition.Z, viewport.PerspectiveAdjustment);

        if (anchorZ <= frontLimit && Fits(anchorZ))
            return;
        if (frontLimit <= backLimit || !CasterIntersectsViewport(caster, casterPosition, viewport))
            return;

        float candidate = MathF.Min(anchorZ, frontLimit);
        if (!Fits(candidate))
        {
            // Sample only the foreground, then refine the nearest safe interval.
            // Bounded work at the edge; ordinary on-screen shadows never enter it.
            float high = candidate;
            bool found = false;
            for (int i = 1; i <= 16; i++)
            {
                float low = candidate + (backLimit - candidate) * (i / 16f);
                if (!Fits(low))
                {
                    high = low;
                    continue;
                }
                for (int iteration = 0; iteration < 14; iteration++)
                {
                    float middle = (low + high) / 2f;
                    if (Fits(middle)) low = middle;
                    else high = middle;
                }
                candidate = low;
                found = true;
                break;
            }
            if (!found)
                return;
        }

        float deltaZ = candidate - anchorZ; // Always inward, never toward the player.
        foreach (var triangle in shadowTriangles)
        {
            triangle.vert1 = Shift(triangle.vert1);
            triangle.vert2 = Shift(triangle.vert2);
            triangle.vert3 = Shift(triangle.vert3);
        }

        Vector3 Shift(IVector3 vertex) => new(vertex.x, vertex.y, vertex.z + deltaZ);

        bool Fits(float z)
        {
            sample.z = z;
            if (!TerrainShadowProjectionHelpers.TryGetHeight(surfaceTriangles, sample, tileBuffer, out float groundY))
                return false;
            sample.y = groundY - surfaceLift;
            return ProjectionMath.TryProjectVertex(sample, surfacePosition.X, surfacePosition.Y,
                surfaceDepth, viewport, out var screen) && double.IsFinite(screen.y) && screen.y <= bottom;
        }
    }

    private static bool CasterIntersectsViewport(OmegaObject3D caster, RenderPosition position, IProjectionViewport viewport)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        double depth = OmegaPerspectiveProjectorFactory.ClampRenderDepth(position.Z, viewport.PerspectiveAdjustment);
        foreach (var part in caster.ObjectParts)
        {
            if (!part.IsVisible)
                continue;
            foreach (var triangle in part.Triangles)
            {
                Include(triangle.vert1);
                Include(triangle.vert2);
                Include(triangle.vert3);
            }
        }
        return minX <= viewport.ScreenWidth && maxX >= 0 && minY <= viewport.ScreenHeight && maxY >= 0;

        void Include(IVector3 point)
        {
            if (!ProjectionMath.TryProjectVertex(point, position.X, position.Y, depth, viewport, out var screen)
                || !double.IsFinite(screen.x) || !double.IsFinite(screen.y))
                return;
            minX = Math.Min(minX, screen.x);
            maxX = Math.Max(maxX, screen.x);
            minY = Math.Min(minY, screen.y);
            maxY = Math.Max(maxY, screen.y);
        }
    }
}
