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
        internal const double MaximumPerspectiveScale = 2.5;

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
            if (!ObjectPlacementHelpers.TryGetRenderPosition(obj, 0, 0, out var x, out var y, out var z))
                return false;

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

            return obj.ObjectName == "Star" || obj.CheckInhabitantVisibility();
        }

        private static bool TryResolveRenderPosition(
            OmegaObject3D obj,
            IProjectionViewport viewport,
            out RenderPosition position)
        {
            if (ObjectPlacementHelpers.TryGetRenderPosition(
                    obj,
                    viewport.ScreenCenterX,
                    viewport.ScreenCenterY,
                    out double screenX,
                    out double screenY,
                    out double screenZ))
            {
                screenZ = ClampRenderDepth(screenZ, viewport.PerspectiveAdjustment);
                position = new RenderPosition(screenX, screenY, screenZ);
                return true;
            }

            position = default;
            return false;
        }

        internal static double ClampRenderDepth(double screenZ, double perspectiveAdjustment)
        {
            if (perspectiveAdjustment <= 0)
                return screenZ;

            // Projection scale = perspectiveAdjustment /
            //                    (screenZ + perspectiveAdjustment).
            // Raising only the render depth prevents the denominator approaching zero.
            double nearestRenderDepth =
                (perspectiveAdjustment / MaximumPerspectiveScale) - perspectiveAdjustment;
            return Math.Max(screenZ, nearestRenderDepth);
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
