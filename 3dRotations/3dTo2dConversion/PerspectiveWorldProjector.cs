using _3dRotations.Helpers;
using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Projection
{
    public class PerspectiveWorldProjector : IWorldProjector<_3dObject, ProjectedTriangleMesh>
    {
        private readonly bool enableLogging = false;
        private readonly IProjectionViewport viewport;
        private readonly PerspectiveProjectionPipeline<ProjectedTriangleMesh> projectionPipeline;

        public PerspectiveWorldProjector() : this(new ScreenSetupProjectionViewport())
        {
        }

        public PerspectiveWorldProjector(IProjectionViewport viewport)
        {
            this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            projectionPipeline = new PerspectiveProjectionPipeline<ProjectedTriangleMesh>(
                this.viewport,
                static () => new ProjectedTriangleMesh());
        }

        public List<ProjectedTriangleMesh> ProjectToTriangles(List<_3dObject> inhabitants, long? currentFrame)
        {
            return ProjectToTriangles(inhabitants, currentFrame, null);
        }

        public List<ProjectedTriangleMesh> ProjectToTriangles(
            List<_3dObject> inhabitants,
            long? currentFrame,
            List<ProjectedTriangleMesh>? reusableResult)
        {
            var screenCoordinates = reusableResult ?? new List<ProjectedTriangleMesh>(inhabitants.Count * 2);
            screenCoordinates.Clear();

            int expectedCapacity = projectionPipeline.EstimateTriangleCapacity(
                inhabitants,
                static obj => obj is _3dObject gameObject && IsVisibleForProjection(gameObject),
                static obj => obj is _3dObject gameObject && gameObject.CrashBoxDebugMode == true);
            if (screenCoordinates.Capacity < expectedCapacity)
                screenCoordinates.Capacity = expectedCapacity;

            foreach (var obj in inhabitants)
            {
                if (obj == null || (obj.ObjectName != "Star" && !obj.CheckInhabitantVisibility())) continue;

                if (!ObjectPlacementHelpers.TryGetRenderPosition(obj, viewport.ScreenCenterX, viewport.ScreenCenterY, out double screenX, out double screenY, out double screenZ))
                    continue;

                //Standard 3d Rendring
                ConvertObjectTo2d(obj, screenX, screenY, screenZ, screenCoordinates);

                if (obj.CrashBoxDebugMode != null && (bool)obj.CrashBoxDebugMode)
                {
                    //Debug visualization of crashboxes
                    ConvertCrashBoxesTo2d(obj, screenX, screenY, screenZ, screenCoordinates);
                }
            }

            return screenCoordinates;
        }

        private static bool IsVisibleForProjection(_3dObject obj)
        {
            return obj.ObjectName == "Star" || obj.CheckInhabitantVisibility();
        }

        private void ConvertObjectTo2d(_3dObject obj, double objPosX, double objPosY, double objPosZ, List<ProjectedTriangleMesh> result)
        {
            if (Logger.ShouldLog(enableLogging) && (obj.ObjectName == "Seeder" || obj.ObjectName == "Lazer"))
            {
                Logger.Log($"Converted 3D object '{obj.ObjectName}' to 2D.");
            }

            projectionPipeline.ConvertObjectTo2d(obj, objPosX, objPosY, objPosZ, result);
        }

        private void ConvertCrashBoxesTo2d(_3dObject obj, double objPosX, double objPosY, double objPosZ, List<ProjectedTriangleMesh> result)
        {
            projectionPipeline.ConvertCrashBoxesTo2d(obj, objPosX, objPosY, objPosZ, result);
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
