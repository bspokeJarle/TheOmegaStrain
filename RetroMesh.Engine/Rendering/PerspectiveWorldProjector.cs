using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public readonly record struct RenderPosition(double X, double Y, double Z);

    public delegate bool TryResolveRenderPosition<TObject>(
        TObject obj,
        IProjectionViewport viewport,
        out RenderPosition position)
        where TObject : class, IRenderable3dObject;

    public sealed class PerspectiveWorldProjector<TObject, TTriangle> : IWorldProjector<TObject, TTriangle>
        where TObject : class, IRenderable3dObject
        where TTriangle : IProjectedTriangle
    {
        private readonly IProjectionViewport viewport;
        private readonly PerspectiveProjectionPipeline<TTriangle> projectionPipeline;
        private readonly TryResolveRenderPosition<TObject> tryResolveRenderPosition;
        private readonly Func<TObject, bool> includeObject;
        private readonly Func<TObject, bool> includeCrashBoxDebug;

        public PerspectiveWorldProjector(
            IProjectionViewport viewport,
            Func<TTriangle> triangleFactory,
            TryResolveRenderPosition<TObject> tryResolveRenderPosition,
            Func<TObject, bool>? includeObject = null,
            Func<TObject, bool>? includeCrashBoxDebug = null)
        {
            this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            this.tryResolveRenderPosition = tryResolveRenderPosition ?? throw new ArgumentNullException(nameof(tryResolveRenderPosition));
            this.includeObject = includeObject ?? IncludeEveryObject;
            this.includeCrashBoxDebug = includeCrashBoxDebug ?? IncludeNoCrashBoxDebug;
            projectionPipeline = new PerspectiveProjectionPipeline<TTriangle>(
                this.viewport,
                triangleFactory ?? throw new ArgumentNullException(nameof(triangleFactory)));
        }

        public List<TTriangle> ProjectToTriangles(List<TObject> renderableObjects, long? currentFrame)
        {
            return ProjectToTriangles(renderableObjects, currentFrame, null);
        }

        public List<TTriangle> ProjectToTriangles(
            List<TObject> renderableObjects,
            long? currentFrame,
            List<TTriangle>? reusableResult)
        {
            var screenCoordinates = reusableResult ?? new List<TTriangle>(renderableObjects.Count * 2);
            screenCoordinates.Clear();

            int expectedCapacity = projectionPipeline.EstimateTriangleCapacity(
                renderableObjects,
                obj => obj is TObject typedObj && includeObject(typedObj),
                obj => obj is TObject typedObj && includeCrashBoxDebug(typedObj));
            ListCapacityHelper.EnsureCapacity(screenCoordinates, expectedCapacity);

            for (int i = 0; i < renderableObjects.Count; i++)
            {
                var obj = renderableObjects[i];
                if (obj == null || !includeObject(obj))
                    continue;

                if (!tryResolveRenderPosition(obj, viewport, out var renderPosition))
                    continue;

                projectionPipeline.ConvertObjectTo2d(
                    obj,
                    renderPosition.X,
                    renderPosition.Y,
                    renderPosition.Z,
                    screenCoordinates);

                if (includeCrashBoxDebug(obj))
                {
                    projectionPipeline.ConvertCrashBoxesTo2d(
                        obj,
                        renderPosition.X,
                        renderPosition.Y,
                        renderPosition.Z,
                        screenCoordinates);
                }
            }

            return screenCoordinates;
        }

        private static bool IncludeEveryObject(TObject obj) => true;

        private static bool IncludeNoCrashBoxDebug(TObject obj) => false;
    }
}
