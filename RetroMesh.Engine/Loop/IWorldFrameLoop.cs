using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public interface IWorldFrameLoop<TWorld, TTriangle>
    {
        List<TTriangle> UpdateWorld(
            TWorld world,
            ref List<TTriangle> projectedCoordinates,
            ref List<TTriangle> crashBoxCoordinates);
    }
}
