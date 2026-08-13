using System.Collections.Generic;

namespace Domain
{
    public interface IWorldFrameLoop<TWorld, TTriangle>
    {
        List<TTriangle> UpdateWorld(
            TWorld world,
            ref List<TTriangle> projectedCoordinates,
            ref List<TTriangle> crashBoxCoordinates);
    }
}
