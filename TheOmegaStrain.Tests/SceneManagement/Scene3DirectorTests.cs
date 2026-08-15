using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Scenes.Scene3;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class Scene3DirectorTests
{
    private Scene3Director _director = null!;
    private GameEventBus _eventBus = null!;
    private TestWorld _world = null!;

    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();

        _director = new Scene3Director();
        _eventBus = new GameEventBus();
        _world = new TestWorld { EventBus = _eventBus };
        _director.Initialize(_eventBus, _world);
    }

    [TestMethod]
    public void Update_DoesNotActivateMotherShip_WhenSeedersRemain()
    {
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("Seeder"));
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 1;

        _director.Update();

        Assert.IsFalse(motherShip.IsActive);
    }

    [TestMethod]
    public void Update_ActivatesMotherShip_WhenAllSeedersAndDronesEliminated()
    {
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 8;

        _director.Update();

        Assert.IsTrue(motherShip.IsActive);
    }

    [TestMethod]
    public void Update_SetsVictory_WhenAllEnemyCountsZeroAndNoLiveEnemies()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 8;
        gps.InitialDrones = 8;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;

        var activeMs = CreateAiObject("MotherShipSmall", isActive: true);
        GameState.SurfaceState.AiObjects.Add(activeMs);

        _director.Update();

        activeMs.ImpactStatus = new ImpactStatus { HasExploded = true };
        gps.MotherShipsRemaining = 0;

        _director.Update();

        Assert.IsTrue(_director.IsVictory);
    }

    [TestMethod]
    public void Update_DoesNotReachMothershipPhase_WhenNoMothershipCandidateExists()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 8;
        gps.InitialDrones = 8;

        var aiObjs = GameState.SurfaceState.AiObjects;
        for (int i = 0; i < 8; i++)
            aiObjs.Add(CreateAiObject("Seeder"));
        for (int i = 0; i < 8; i++)
            aiObjs.Add(CreateAiObject("KamikazeDrone", isActive: true));

        aiObjs.RemoveAll(o => o.ObjectName == "Seeder" || o.ObjectName == "KamikazeDrone");

        _director.Update();

        Assert.AreEqual(0, gps.MotherShipsRemaining,
            "Without a mothership candidate in AiObjects, scene cannot enter mothership phase.");
        Assert.IsFalse(_director.IsVictory);
    }

    [TestMethod]
    public void Update_FullPhaseProgression_Scene3()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 8;
        gps.InitialDrones = 8;

        var aiObjs = GameState.SurfaceState.AiObjects;
        var seeders = new List<_3dObject>();
        for (int i = 0; i < 8; i++)
        {
            var s = CreateAiObject("Seeder");
            seeders.Add(s);
            aiObjs.Add(s);
        }

        var drones = new List<_3dObject>();
        for (int i = 0; i < 8; i++)
        {
            var d = CreateAiObject("KamikazeDrone", isActive: false);
            drones.Add(d);
            aiObjs.Add(d);
        }

        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        aiObjs.Add(motherShip);

        _director.Update();
        Assert.IsTrue(drones.TrueForAll(d => !d.IsActive));
        Assert.IsFalse(motherShip.IsActive);

        gps.PowerUpsCollected = 1;
        _director.Update();
        Assert.IsTrue(drones.TrueForAll(d => d.IsActive));
        Assert.IsFalse(motherShip.IsActive);

        foreach (var s in seeders) aiObjs.Remove(s);
        foreach (var d in drones) aiObjs.Remove(d);
        _director.Update();
        Assert.IsTrue(motherShip.IsActive);
        Assert.IsFalse(_director.IsVictory);

        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;
        motherShip.ImpactStatus = new ImpactStatus { HasExploded = true };
        _director.Update();

        Assert.IsTrue(_director.IsVictory);
    }

    private static int _nextObjectId = 2000;

    private static _3dObject CreateAiObject(string name, bool isActive = true)
    {
        return new _3dObject
        {
            ObjectId = _nextObjectId++,
            ObjectName = name,
            IsActive = isActive
        };
    }

    private class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; }
        public bool IsPaused { get; set; }
    }
}
