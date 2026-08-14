using _3dRotations.Helpers;
using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonSetup;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Projection
{
    public static class OmegaPerspectiveProjectorFactory
    {
        public static IWorldProjector<_3dObject, ProjectedTriangleMesh> Create()
        {
            return Create(new ScreenSetupProjectionViewport());
        }

        public static IWorldProjector<_3dObject, ProjectedTriangleMesh> Create(IProjectionViewport viewport)
        {
            return new PerspectiveWorldProjector<_3dObject, ProjectedTriangleMesh>(
                viewport,
                static () => new ProjectedTriangleMesh(),
                TryResolveRenderPosition,
                static obj => obj.ObjectName == "Star" || obj.CheckInhabitantVisibility(),
                static obj => obj.CrashBoxDebugMode == true);
        }

        private static bool TryResolveRenderPosition(
            _3dObject obj,
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
                position = new RenderPosition(screenX, screenY, screenZ);
                return true;
            }

            position = default;
            return false;
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
