using System.Collections.Generic;

namespace Domain
{
    public interface I3dWorld : IRenderable3dWorldView
    {
        List<I3dObject> WorldInhabitants { get; set; }
        ISceneHandler SceneHandler { get; set; }
        IGameEventBus? EventBus { get; set; }

        IEnumerable<IRenderable3dObject> IRenderable3dWorldView.RenderableObjects => WorldInhabitants;
    }
}
