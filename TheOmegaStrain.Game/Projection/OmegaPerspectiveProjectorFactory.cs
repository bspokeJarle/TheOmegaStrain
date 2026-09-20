using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;

namespace TheOmegaStrain.Game.Projection
{
    public static class OmegaPerspectiveProjectorFactory
    {
        // Keep close objects readable without changing their world position, physics,
        // collision or weapon origins. At the cap an object is 2.5 times its normal
        // perspective size (before the normal ObjectZoom is applied).
        internal const double MaximumPerspectiveScale = SurfaceRenderAnchorHelpers.MaximumPerspectiveScale;

        /// <summary>
        /// Converts a caster's render anchor to Surface-local shadow coordinates.
        /// Both inputs are render positions, with the same screen-centre convention.
        /// The returned X/Z can be used to sample ground Y from the rotated Surface.
        /// </summary>
        public static bool TryGetSurfaceLocalShadowAnchor(
            RenderPosition casterPosition,
            RenderPosition surfacePosition,
            out float localX,
            out float localZ)
        {
            return TryGetSurfaceLocalShadowAnchor(casterPosition, surfacePosition, out localX, out _, out localZ);
        }

        /// <summary>
        /// Also returns the caster's Y in Surface vertex units, so its clearance
        /// can be measured against sampled ground Y without mixing in pixel scale.
        /// This Y is the caster's height coordinate, not the ground height.
        /// </summary>
        public static bool TryGetSurfaceLocalShadowAnchor(
            RenderPosition casterPosition,
            RenderPosition surfacePosition,
            out float localX,
            out float localY,
            out float localZ)
        {
            return SurfaceRenderAnchorHelpers.TryGetSurfaceLocalAnchor(
                casterPosition, surfacePosition, out localX, out localY, out localZ);
        }

        /// <summary>
        /// Projects a collision centre for HUD markers. Authoritative AI objects have
        /// unrotated geometry; pass false to rotate only the calculated centre.
        /// Returns its unscaled render-space centre too, so detection includes current offsets.
        /// </summary>
        public static bool TryProjectCrashCenter(
            OmegaObject3D obj, out Vector3 renderCenter, out Vector3 screenCenter, bool geometryAlreadyRotated = true)
        {
            renderCenter = new Vector3();
            screenCenter = new Vector3();
            if (!ObjectPlacementHelpers.TryGetRenderPosition(
                    obj, 0, 0, out double rawX, out double rawY, out double rawZ))
                return false;

            var position = SurfaceSlopeRenderPositionHelpers.Apply(
                obj,
                new RenderPosition(rawX, rawY, rawZ));

            double x = position.X;
            double y = position.Y;
            double z = position.Z;

            // Frame and live weapon boxes are already rotated; AI template boxes are not.
            var localCenter = geometryAlreadyRotated
                ? ObjectCollisionGeometry.GetLocalCrashCenter(obj)
                : ObjectCollisionGeometry.GetRotatedLocalCrashCenter(obj);
            renderCenter = new Vector3((float)x + localCenter.x, (float)y + localCenter.y, (float)z + localCenter.z);
            if (!ProjectionMath.TryProjectVertex(localCenter,
                    x + ScreenSetup.screenSizeX / 2, y + ScreenSetup.screenSizeY / 2,
                    ClampRenderDepth(z, ScreenSetup.perspectiveAdjustment),
                    ScreenSetup.perspectiveAdjustment, ScreenSetup.defaultObjectZoom, out var projected))
                return false;

            screenCenter = new Vector3((float)projected.x, (float)projected.y, 0f);
            return float.IsFinite(renderCenter.x) && float.IsFinite(renderCenter.y) && float.IsFinite(renderCenter.z)
                && float.IsFinite(screenCenter.x) && float.IsFinite(screenCenter.y);
        }

        /// <summary>
        /// Uses the renderer's own projection to verify that visible object geometry
        /// intersects the viewport. The broader world-distance visibility check keeps
        /// objects ready to enter the frame; it must not by itself make invisible
        /// geometry eligible for collisions.
        /// </summary>
        public static bool IntersectsViewport(OmegaObject3D obj)
        {
            var viewport = new ScreenSetupProjectionViewport();
            if (!TryResolveRenderPosition(obj, viewport, out var position))
                return false;

            return IntersectsViewport(obj, position, viewport);
        }

        public static bool IntersectsViewport(
            OmegaObject3D obj,
            RenderPosition position,
            IProjectionViewport viewport)
        {
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            double depth = ClampRenderDepth(position.Z, viewport.PerspectiveAdjustment);

            foreach (var part in obj.ObjectParts)
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

            return minX <= viewport.ScreenWidth && maxX >= 0 &&
                   minY <= viewport.ScreenHeight && maxY >= 0;

            void Include(IVector3 point)
            {
                if (!ProjectionMath.TryProjectVertex(
                        point,
                        position.X,
                        position.Y,
                        depth,
                        viewport,
                        out var screen) ||
                    !double.IsFinite(screen.x) ||
                    !double.IsFinite(screen.y))
                {
                    return;
                }

                minX = Math.Min(minX, screen.x);
                maxX = Math.Max(maxX, screen.x);
                minY = Math.Min(minY, screen.y);
                maxY = Math.Max(maxY, screen.y);
            }
        }

        public static IWorldProjector<OmegaObject3D, ProjectedTriangleMesh> Create()
        {
            return Create(new ScreenSetupProjectionViewport());
        }

        public static IWorldProjector<OmegaObject3D, ProjectedTriangleMesh> Create(IProjectionViewport viewport)
        {
            return new PerspectiveWorldProjector<OmegaObject3D, ProjectedTriangleMesh>(
                viewport,
                static () => new ProjectedTriangleMesh(),
                TryResolveRenderPosition,
                ShouldProjectObject,
                static obj => obj.CrashBoxDebugMode == true);
        }

        private static bool ShouldProjectObject(OmegaObject3D obj)
        {
            // Rockets travel through ObjectOffsets; WorldPosition remains their launch
            // anchor. Their particles inherit that anchor too. Let normal screen/depth
            // culling use their actual render position, not distance to the launch point.
            if (obj.ObjectName is "Rocket" or "EnemyRocket" ||
                (obj.ObjectName == "Particle" && obj.ImpactStatus?.ObjectName is "Rocket" or "EnemyRocket"))
                return true;

            // LiveGameLoop owns the frame's culling decision, including its short
            // exit hold. Do not immediately undo that decision with another range test.
            return obj.ObjectName == "Star" || obj.IsOnScreen || obj.CheckInhabitantVisibility();
        }

        private static bool TryResolveRenderPosition(
            OmegaObject3D obj,
            IProjectionViewport viewport,
            out RenderPosition position)
        {
            return TryResolveRenderPosition(
                obj,
                viewport.ScreenCenterX,
                viewport.ScreenCenterY,
                viewport.PerspectiveAdjustment,
                out position);
        }

        internal static bool TryResolveRenderPosition(
            OmegaObject3D obj,
            int screenCenterX,
            int screenCenterY,
            double perspectiveAdjustment,
            out RenderPosition position)
        {
            if (ObjectPlacementHelpers.TryGetRenderPosition(
                    obj,
                    screenCenterX,
                    screenCenterY,
                    out double screenX,
                    out double screenY,
                    out double screenZ))
            {
                screenZ = ClampRenderDepth(screenZ, perspectiveAdjustment);
                position = SurfaceSlopeRenderPositionHelpers.Apply(
                    obj,
                    new RenderPosition(screenX, screenY, screenZ));
                return true;
            }

            position = default;
            return false;
        }

        // Shared with shadow viewport checks so near objects use the renderer's
        // exact depth cap, rather than a second approximation of perspective.
        public static double ClampRenderDepth(double screenZ, double perspectiveAdjustment)
        {
            return SurfaceRenderAnchorHelpers.ClampRenderDepth(screenZ, perspectiveAdjustment);
        }

        private sealed class ScreenSetupProjectionViewport : IProjectionViewport
        {
            public int ScreenWidth => ScreenSetup.screenSizeX;
            public int ScreenHeight => ScreenSetup.screenSizeY;
            public int ScreenCenterX => ScreenWidth / 2;
            public int ScreenCenterY => ScreenHeight / 2;
            public double PerspectiveAdjustment => ScreenSetup.perspectiveAdjustment;
            public double ObjectZoom => ScreenSetup.defaultObjectZoom;
        }
    }
}
