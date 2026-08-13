using System.Collections.Generic;

namespace Domain
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
