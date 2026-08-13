using System.Collections.Generic;

namespace Domain
{
    public interface IGameLoop<TTriangle> : IWorldFrameLoop<I3dWorld, TTriangle>
    {
        string DebugMessage { get; set; }
        bool FadeOutWorld { get; set; }
        bool FadeInWorld { get; set; }
        bool SceneResetReady { get; set; }
        I3dObject ShipCopy { get; set; }
        I3dObject SurfaceCopy { get; set; }
    }
}
