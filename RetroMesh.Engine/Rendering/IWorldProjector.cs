using System.Collections.Generic;

namespace Domain
{
    public interface IWorldProjector<TObject, TTriangle>
    {
        List<TTriangle> ConvertTo2dFromObjects(
            List<TObject> renderableObjects,
            long? currentFrame);

        List<TTriangle> ConvertTo2dFromObjects(
            List<TObject> renderableObjects,
            long? currentFrame,
            List<TTriangle>? reusableResult);
    }
}
