using _3dTesting._Coordinates;
using _3dTesting.MainWindowClasses.Loops;
using Domain;
using System.Collections.Generic;

namespace _3dTesting.MainWindowClasses
{
    public class GameWorldManager
    {
        private readonly IGameLoop<_2dTriangleMesh> liveLoop;
        private IGameLoop<_2dTriangleMesh> currentLoop;

        public GameWorldManager()
        {
            liveLoop = new LiveGameLoop();
            currentLoop = liveLoop;
        }

        public GameWorldManager(IGameLoop<_2dTriangleMesh> gameLoop)
        {
            liveLoop = gameLoop;
            currentLoop = gameLoop;
        }

        private IGameLoop<_2dTriangleMesh> GetActiveLoop(I3dWorld world)
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

        public List<_2dTriangleMesh> UpdateWorld(I3dWorld world, ref List<_2dTriangleMesh> projectedCoordinates, ref List<_2dTriangleMesh> crashBoxCoordinates)
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
