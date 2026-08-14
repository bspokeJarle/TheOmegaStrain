using Domain;
using System.Collections.Generic;

namespace TheOmegaStrain.Runtime.Loops
{
    public class GameWorldManager
    {
        private readonly IGameLoop<ProjectedTriangleMesh> liveLoop;
        private IGameLoop<ProjectedTriangleMesh> currentLoop;

        public GameWorldManager()
        {
            liveLoop = new LiveGameLoop();
            currentLoop = liveLoop;
        }

        public GameWorldManager(IGameLoop<ProjectedTriangleMesh> gameLoop)
        {
            liveLoop = gameLoop;
            currentLoop = gameLoop;
        }

        private IGameLoop<ProjectedTriangleMesh> GetActiveLoop(I3dWorld world)
        {
            currentLoop = liveLoop;
            return currentLoop;
        }

        public string DebugMessage
        {
            get => currentLoop.DebugMessage;
            set => currentLoop.DebugMessage = value;
        }

        public bool FadeOutWorld
        {
            get => currentLoop.FadeOutWorld;
            set => currentLoop.FadeOutWorld = value;
        }

        public bool FadeInWorld
        {
            get => currentLoop.FadeInWorld;
            set => currentLoop.FadeInWorld = value;
        }

        public bool SceneResetReady
        {
            get => currentLoop.SceneResetReady;
            set => currentLoop.SceneResetReady = value;
        }

        public I3dObject ShipCopy
        {
            get => currentLoop.ShipCopy;
            set => currentLoop.ShipCopy = value;
        }

        public I3dObject SurfaceCopy
        {
            get => currentLoop.SurfaceCopy;
            set => currentLoop.SurfaceCopy = value;
        }

        public List<ProjectedTriangleMesh> UpdateWorld(I3dWorld world, ref List<ProjectedTriangleMesh> projectedCoordinates, ref List<ProjectedTriangleMesh> crashBoxCoordinates)
        {
            return GetActiveLoop(world).UpdateWorld(world, ref projectedCoordinates, ref crashBoxCoordinates);
        }

        public void StopNonMusicAudio()
        {
            if (liveLoop is LiveGameLoop liveGameLoop)
                liveGameLoop.StopNonMusicAudio();
        }

        public void UpdatePausedVictoryReward(I3dWorld world)
        {
            if (liveLoop is LiveGameLoop liveGameLoop)
                liveGameLoop.UpdatePausedVictoryReward(world);
        }
    }
}
