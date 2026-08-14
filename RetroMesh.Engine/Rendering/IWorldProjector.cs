using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public interface IWorldProjector<TObject, TTriangle>
    {
        List<TTriangle> ProjectToTriangles(
            List<TObject> renderableObjects,
            long? currentFrame);

        List<TTriangle> ProjectToTriangles(
            List<TObject> renderableObjects,
            long? currentFrame,
            List<TTriangle>? reusableResult);
    }
}
