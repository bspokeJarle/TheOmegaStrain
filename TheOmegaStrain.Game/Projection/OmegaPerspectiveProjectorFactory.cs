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
