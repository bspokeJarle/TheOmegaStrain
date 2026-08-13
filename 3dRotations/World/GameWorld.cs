using CommonUtilities.CommonGlobalState;
using CommonUtilities.Events;
using Domain;
using _3dRotations.SceneManagement;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World
{
    //This class will contain all the objects in the world and the world itself
    public class GameWorld : I3dWorld
    {
        //Global class to hold all the global variables and methods
        public List<I3dObject> WorldInhabitants { get; set; } = new List<I3dObject>();
        //SceneHandler to handle the scenes in the game
        public ISceneHandler SceneHandler { get; set; } = new SceneHandler();
        public IGameEventBus? EventBus { get; set; } = new GameEventBus();

        public GameWorld()
        {
            GameState.EventBus = EventBus;

            //Initialize the world with Scene1 (should be Intro later)
            SceneHandler.SetupActiveScene(this);
        }

        public bool IsPaused { get; set; } = false;
    }

}
