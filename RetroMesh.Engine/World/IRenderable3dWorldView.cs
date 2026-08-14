using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public interface IRenderable3dWorldView
    {
        IEnumerable<IRenderable3dObject> RenderableObjects { get; }
        bool IsPaused { get; set; }
    }
}
