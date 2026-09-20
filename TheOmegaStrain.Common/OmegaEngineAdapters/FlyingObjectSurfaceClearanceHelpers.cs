using RetroMesh.Engine;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Common.OmegaEngineAdapters;

public static class FlyingObjectSurfaceClearanceHelpers
{
    public const float ClearanceHoldSeconds = 2f;
    public const float ClearanceReleaseUnitsPerSecond = 120f;

    public static bool ApplyMinimumClearance(I3dObject obj)
    {
        float minimumClearance = TerrainAvoidanceSetup.GetMinimumSurfaceClearance(obj.ObjectName);
        return ApplyMinimumClearance(obj, minimumClearance);
    }

    public static bool ApplyMinimumClearance(I3dObject obj, float minimumClearance)
    {
        float requiredLift = CalculateRequiredLift(obj, minimumClearance) ?? 0f;
        if (requiredLift <= 0f || obj.ObjectOffsets == null)
            return false;

        obj.ObjectOffsets.y -= requiredLift;
        return true;
    }

    public static bool ApplyMinimumClearance(
        I3dObject obj,
        FlyingObjectSurfaceClearanceState state,
        float deltaSeconds)
    {
        float? requiredLift = CalculateRequiredLift(
            obj,
            TerrainAvoidanceSetup.GetMinimumSurfaceClearance(obj.ObjectName));
        // Missing terrain is unknown, not evidence that it is safe to descend.
        float appliedLift = requiredLift.HasValue
            ? state.Update(requiredLift.Value, deltaSeconds)
            : state.RetainedLift;
        if (appliedLift <= 0f || obj.ObjectOffsets == null)
            return false;

        obj.ObjectOffsets.y -= appliedLift;
        return true;
    }

    private static float? CalculateRequiredLift(I3dObject obj, float minimumClearance)
    {
        if (!obj.IsOnScreen || minimumClearance <= 0f ||
            obj is not OmegaObject3D omegaObject || obj.ObjectOffsets == null)
            return null;

        var surfaceOffsets = GameState.SurfaceState.SurfaceViewportObject?.ObjectOffsets;
        var rotatedTiles = obj.ParentSurface?.RotatedSurfaceTriangles;
        var localWorld = omegaObject.GetLocalWorldPosition();
        if (surfaceOffsets == null || localWorld == null || rotatedTiles == null || rotatedTiles.Count == 0)
            return null;

        float objectScreenX = -localWorld.x + obj.ObjectOffsets.x;
        float objectScreenZ = localWorld.z + obj.ObjectOffsets.z;
        // Compare terrain with the same visible Y used by the projector. At the
        // Normal camera angle the slope correction can otherwise move an airborne
        // object down into the foreground terrain after clearance was approved.
        float objectScreenY = -localWorld.y + obj.ObjectOffsets.y
            + (float)SurfaceSlopeRenderPositionHelpers.GetCorrectionY(
                omegaObject, omegaObject.WorldPosition);
        if (!SurfaceRenderAnchorHelpers.TryGetSurfaceLocalAnchor(
                new RenderPosition(objectScreenX, objectScreenY, objectScreenZ),
                new RenderPosition(surfaceOffsets.x, surfaceOffsets.y, surfaceOffsets.z),
                out float surfaceLocalX, out float objectSurfaceLocalY, out float surfaceLocalZ,
                out float objectProjectionScale))
            return null;
        float footprintRadius = GetVisibleFootprintRadius(obj);
        float projectedTileDepth = SurfaceSetup.tileSize *
            MathF.Sin(WorldViewSetup.SurfacePitchDegrees * MathF.PI / 180f);
        ReadOnlySpan<(float X, float Z)> sampleOffsets =
        [
            (0f, 0f),
            (-footprintRadius, 0f),
            (footprintRadius, 0f),
            (0f, -footprintRadius),
            (0f, footprintRadius),
            // A footprint sample can miss a sharp platform edge. These two
            // samples inspect the corresponding point in the adjacent Surface
            // tile, one tile toward the horizon and one toward the camera.
            (0f, -projectedTileDepth),
            (0f, projectedTileDepth)
        ];

        float maximumRequiredLift = 0f;
        bool foundGround = false;
        for (int i = 0; i < sampleOffsets.Length; i++)
        {
            float sampleX = surfaceLocalX + sampleOffsets[i].X;
            float sampleZ = surfaceLocalZ + sampleOffsets[i].Z;
            if (!SurfaceGroundProjectionHelpers.IsWithinSurfaceBounds(rotatedTiles, sampleX, sampleZ) ||
                !SurfaceGroundProjectionHelpers.TryGetSurfaceGroundPoint(
                    rotatedTiles, sampleX, sampleZ, out _, out float groundY, out _))
                continue;

            foundGround = true;
            float requiredSurfaceLift = minimumClearance - (groundY - objectSurfaceLocalY);
            maximumRequiredLift = MathF.Max(
                maximumRequiredLift, requiredSurfaceLift * objectProjectionScale);
        }

        // Visible arrivals can lie beyond the finite Surface patch. The existing
        // projection helper falls back to the nearest tile's mean height there.
        // Use that reference only when none of the footprint samples hit terrain.
        if (!foundGround)
        {
            if (!SurfaceGroundProjectionHelpers.TryGetSurfaceGroundPoint(
                rotatedTiles, surfaceLocalX, surfaceLocalZ, out _, out float edgeY, out _))
                return null;
            maximumRequiredLift = MathF.Max(0f,
                (minimumClearance - (edgeY - objectSurfaceLocalY)) * objectProjectionScale);
        }

        return maximumRequiredLift;
    }

    private static float GetVisibleFootprintRadius(I3dObject obj)
    {
        float radiusSquared = 0f;
        for (int partIndex = 0; partIndex < obj.ObjectParts.Count; partIndex++)
        {
            var part = obj.ObjectParts[partIndex];
            if (!part.IsVisible || part.Triangles == null)
                continue;

            for (int triangleIndex = 0; triangleIndex < part.Triangles.Count; triangleIndex++)
            {
                var triangle = part.Triangles[triangleIndex];
                Accumulate(triangle.vert1);
                Accumulate(triangle.vert2);
                Accumulate(triangle.vert3);
            }
        }

        return MathF.Sqrt(radiusSquared);

        void Accumulate(IVector3 vertex)
        {
            radiusSquared = MathF.Max(radiusSquared, vertex.x * vertex.x + vertex.z * vertex.z);
        }
    }
}

public sealed class FlyingObjectSurfaceClearanceState
{
    public float RetainedLift { get; private set; }
    public float HoldSecondsRemaining { get; private set; }

    public float Update(float requiredLift, float deltaSeconds)
    {
        float dt = Math.Clamp(deltaSeconds, 0f, 0.1f);
        requiredLift = MathF.Max(0f, requiredLift);

        if (requiredLift >= RetainedLift)
        {
            RetainedLift = requiredLift;
            HoldSecondsRemaining = FlyingObjectSurfaceClearanceHelpers.ClearanceHoldSeconds;
            return RetainedLift;
        }

        if (HoldSecondsRemaining > 0f)
        {
            HoldSecondsRemaining = MathF.Max(0f, HoldSecondsRemaining - dt);
            return RetainedLift;
        }

        float difference = RetainedLift - requiredLift;
        RetainedLift = difference <= 10f
            ? requiredLift
            : MathF.Max(requiredLift, RetainedLift -
                FlyingObjectSurfaceClearanceHelpers.ClearanceReleaseUnitsPerSecond * dt);
        return RetainedLift;
    }
}
