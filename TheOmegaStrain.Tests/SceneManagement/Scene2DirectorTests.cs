using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Scenes.Scene2;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class Scene2DirectorTests
{
    private Scene2Director _director = null!;
    private GameEventBus _eventBus = null!;
    private TestWorld _world = null!;

    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();

        _director = new Scene2Director();
        _eventBus = new GameEventBus();
        _world = new TestWorld { EventBus = _eventBus };
        _director.Initialize(_eventBus, _world);
    }

    // -----------------------------------------------------------------
    // Initial state
    // -----------------------------------------------------------------

    [TestMethod]
    public void Initialize_SetsVictoryAndDefeatToFalse()
    {
        Assert.IsFalse(_director.IsVictory);
        Assert.IsFalse(_director.IsDefeat);
    }

    // -----------------------------------------------------------------
    // Drone activation phase
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_DoesNotActivateDrones_WhenDecoyNotUnlocked()
    {
        var drone = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(drone);
        GameState.GamePlayState.PowerUpsCollected = 0;

        _director.Update();

        Assert.IsFalse(drone.IsActive);
    }

    [TestMethod]
    public void Update_ActivatesDrones_WhenDecoyUnlocked()
    {
        var drone1 = CreateAiObject("KamikazeDrone", isActive: false);
        var drone2 = CreateAiObject("KamikazeDrone", isActive: false);
        var drone3 = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(drone1);
        GameState.SurfaceState.AiObjects.Add(drone2);
        GameState.SurfaceState.AiObjects.Add(drone3);
        GameState.GamePlayState.PowerUpsCollected = 1;

        _director.Update();

        Assert.IsTrue(drone1.IsActive);
        Assert.IsTrue(drone2.IsActive);
        Assert.IsTrue(drone3.IsActive);
    }

    [TestMethod]
    public void Update_ActivatesDronesOnlyOnce()
    {
        var drone = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(drone);
        GameState.GamePlayState.PowerUpsCollected = 1;

        _director.Update();
        Assert.IsTrue(drone.IsActive);

        var lateDrone = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(lateDrone);
        _director.Update();

        Assert.IsFalse(lateDrone.IsActive, "Late drone should not be activated after first activation pass.");
    }

    // -----------------------------------------------------------------
    // Mothership activation phase
    // -----------------------------------------------------------------

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
    public void Update_DoesNotActivateMotherShip_WhenDronesRemain()
    {
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("KamikazeDrone"));
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
        GameState.GamePlayState.InitialSeeders = 4;

        _director.Update();

        Assert.IsTrue(motherShip.IsActive);
    }

    [TestMethod]
    public void Update_MotherShipActivation_SetsRemainingCounts()
    {
        var ms1 = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(ms1);
        GameState.GamePlayState.InitialSeeders = 4;

        _director.Update();

        var gps = GameState.GamePlayState;
        Assert.AreEqual(0, gps.SeedersRemaining);
        Assert.AreEqual(0, gps.DronesRemaining);
        Assert.AreEqual(1, gps.MotherShipsRemaining);
    }

    [TestMethod]
    public void Update_MotherShipActivation_SavesCheckpoint()
    {
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 4;
        GameState.GamePlayState.Score = 1200;

        _director.Update();

        Assert.IsTrue(GameState.GamePlayState.HasCheckpoint);
        Assert.AreEqual(1200L, GameState.GamePlayState.CheckpointScore);
    }

    [TestMethod]
    public void Update_DoesNotActivateMotherShip_WhenInitialSeedersIsZero()
    {
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        var seeder = CreateAiObject("Seeder", isActive: true);
        GameState.SurfaceState.AiObjects.Add(seeder);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 0;

        _director.Update();

        Assert.IsFalse(motherShip.IsActive, "MotherShip should not activate while live seeders remain.");
    }

    // -----------------------------------------------------------------
    // Victory condition
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_SetsVictory_WhenAllEnemyCountsZeroAndNoLiveEnemies()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 4;
        gps.InitialDrones = 3;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;

        var activeMs = CreateAiObject("MotherShipSmall", isActive: true);
        GameState.SurfaceState.AiObjects.Add(activeMs);

        // Only non-enemy or exploded objects in AiObjects
        var exploded = CreateAiObject("MotherShipSmall");
        exploded.ImpactStatus = new ImpactStatus { HasExploded = true };
        GameState.SurfaceState.AiObjects.Add(exploded);
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("SpaceSwan"));

        _director.Update();
        activeMs.ImpactStatus = new ImpactStatus { HasExploded = true };
        gps.MotherShipsRemaining = 0;
        _director.Update();

        Assert.IsTrue(_director.IsVictory);
    }

    [TestMethod]
    public void Update_DoesNotSetVictory_WhenLiveMotherShipRemainsInAiObjects()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 4;
        gps.InitialDrones = 3;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        // Live (non-exploded) mothership still in AiObjects
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("MotherShipSmall"));

        _director.Update();

        Assert.IsFalse(_director.IsVictory);
    }

    [TestMethod]
    public void Update_DoesNotSetVictory_WhenSeedersRemainingNonZero()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 4;
        gps.InitialDrones = 3;
        gps.SeedersRemaining = 1;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        _director.Update();

        Assert.IsFalse(_director.IsVictory);
    }

    [TestMethod]
    public void Update_DoesNotSetVictory_WhenInitialCountsAreZero()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 0;
        gps.InitialDrones = 0;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        _director.Update();

        Assert.IsFalse(_director.IsVictory);
    }

    // -----------------------------------------------------------------
    // Update short-circuit
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_SkipsChecks_WhenAlreadyVictory()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 1;
        gps.InitialDrones = 1;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;
        var motherShip = CreateAiObject("MotherShipSmall", isActive: true);
        GameState.SurfaceState.AiObjects.Add(motherShip);

        _director.Update();

        motherShip.ImpactStatus = new ImpactStatus { HasExploded = true };
        gps.MotherShipsRemaining = 0;
        _director.Update();

        Assert.IsTrue(_director.IsVictory);

        // Adding enemies after victory should not change the result
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("Seeder"));
        gps.SeedersRemaining = 1;
        _director.Update();

        Assert.IsTrue(_director.IsVictory, "Victory should persist once set.");
    }

    // -----------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------

    [TestMethod]
    public void Dispose_ResetsState()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 1;
        gps.InitialDrones = 1;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;
        var motherShip = CreateAiObject("MotherShipSmall", isActive: true);
        GameState.SurfaceState.AiObjects.Add(motherShip);

        _director.Update();

        motherShip.ImpactStatus = new ImpactStatus { HasExploded = true };
        gps.MotherShipsRemaining = 0;
        _director.Update();

        Assert.IsTrue(_director.IsVictory);

        _director.Dispose();

        Assert.IsFalse(_director.IsVictory);
        Assert.IsFalse(_director.IsDefeat);
    }

    // -----------------------------------------------------------------
    // Full phase progression (Scene2: more enemies than Scene1)
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_FullPhaseProgression_Scene2()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 4;
        gps.InitialDrones = 3;

        // Scene2 typically has more enemies
        var aiObjs = GameState.SurfaceState.AiObjects;
        var seeders = new List<_3dObject>();
        for (int i = 0; i < 4; i++)
        {
            var s = CreateAiObject("Seeder");
            seeders.Add(s);
            aiObjs.Add(s);
        }
        var drones = new List<_3dObject>();
        for (int i = 0; i < 3; i++)
        {
            var d = CreateAiObject("KamikazeDrone", isActive: false);
            drones.Add(d);
            aiObjs.Add(d);
        }
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        aiObjs.Add(motherShip);

        // Phase 1: nothing activated yet
        _director.Update();
        Assert.IsTrue(drones.TrueForAll(d => !d.IsActive));
        Assert.IsFalse(motherShip.IsActive);
        Assert.IsFalse(_director.IsVictory);

        // Phase 2: unlock decoy -> activate all drones
        gps.PowerUpsCollected = 1;
        _director.Update();
        Assert.IsTrue(drones.TrueForAll(d => d.IsActive));
        Assert.IsFalse(motherShip.IsActive);

        // Phase 3: remove all seeders and drones -> activate mothership
        foreach (var s in seeders) aiObjs.Remove(s);
        foreach (var d in drones) aiObjs.Remove(d);
        _director.Update();
        Assert.IsTrue(motherShip.IsActive);
        Assert.IsFalse(_director.IsVictory);

        // Phase 4: defeat mothership -> victory
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;
        motherShip.ImpactStatus = new ImpactStatus { HasExploded = true };
        _director.Update();
        Assert.IsTrue(_director.IsVictory);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static int _nextObjectId = 1000;

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

    // -----------------------------------------------------------------
    // Re-initialization after Dispose
    // -----------------------------------------------------------------

    [TestMethod]
    public void Initialize_AfterDispose_ResetsPhaseFlags()
    {
        GameState.GamePlayState.InitialSeeders = 1;
        GameState.GamePlayState.PowerUpsCollected = 1;
        var ms = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(ms);
        _director.Update();
        Assert.IsTrue(ms.IsActive);

        _director.Dispose();
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        _director.Initialize(_eventBus, _world);

        var newDrone = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(newDrone);
        GameState.GamePlayState.PowerUpsCollected = 1;
        _director.Update();

        Assert.IsTrue(newDrone.IsActive, "After re-initialize, drone activation should work again.");
    }

    // -----------------------------------------------------------------
    // Mothership activation idempotency
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_MotherShipActivation_OnlyHappensOnce()
    {
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 4;
        GameState.GamePlayState.Score = 800;

        _director.Update();
        Assert.AreEqual(800L, GameState.GamePlayState.CheckpointScore);

        GameState.GamePlayState.Score = 9999;
        _director.Update();
        Assert.AreEqual(800L, GameState.GamePlayState.CheckpointScore,
            "Checkpoint should not be overwritten by subsequent Updates.");
    }

    // -----------------------------------------------------------------
    // Victory with mixed exploded enemies
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_Victory_ExplodedEnemiesDoNotBlock()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 4;
        gps.InitialDrones = 3;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        var exSeeder = CreateAiObject("Seeder");
        exSeeder.ImpactStatus = new ImpactStatus { HasExploded = true };
        var exDrone = CreateAiObject("KamikazeDrone");
        exDrone.ImpactStatus = new ImpactStatus { HasExploded = true };
        var exMs = CreateAiObject("MotherShipSmall");
        exMs.ImpactStatus = new ImpactStatus { HasExploded = true };
        GameState.SurfaceState.AiObjects.Add(exSeeder);
        GameState.SurfaceState.AiObjects.Add(exDrone);
        GameState.SurfaceState.AiObjects.Add(exMs);

        _director.Update();

        Assert.IsTrue(_director.IsVictory);
    }

    [TestMethod]
    public void Update_Victory_NonEnemyObjectsDoNotBlock()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 1;
        gps.InitialDrones = 1;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;

        var motherShip = CreateAiObject("MotherShipSmall", isActive: true);
        GameState.SurfaceState.AiObjects.Add(motherShip);

        GameState.SurfaceState.AiObjects.Add(CreateAiObject("Tree"));
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("SpaceSwan"));
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("Tower"));

        _director.Update();

        motherShip.ImpactStatus = new ImpactStatus { HasExploded = true };
        gps.MotherShipsRemaining = 0;
        _director.Update();

        Assert.IsTrue(_director.IsVictory);
    }

    // -----------------------------------------------------------------
    // All conditions met simultaneously
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_AllConditionsMetSimultaneously_ReachesVictory()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 1;
        gps.InitialDrones = 1;
        gps.PowerUpsCollected = 1;

        var ms = CreateAiObject("MotherShipSmall", isActive: true);
        ms.ImpactStatus = new ImpactStatus { HasExploded = true };
        GameState.SurfaceState.AiObjects.Add(ms);

        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        _director.Update();

        Assert.IsTrue(_director.IsVictory);
    }

    // -----------------------------------------------------------------
    // Empty AiObjects resilience
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_EmptyAiObjects_DoesNotCrash()
    {
        GameState.SurfaceState.AiObjects.Clear();
        GameState.GamePlayState.InitialSeeders = 0;

        _director.Update();

        Assert.IsFalse(_director.IsVictory);
        Assert.IsFalse(_director.IsDefeat);
    }

    // -----------------------------------------------------------------
    // Live enemy blocks victory even with counts at zero
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_DoesNotSetVictory_WhenLiveDroneInAiObjects()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 1;
        gps.InitialDrones = 1;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        GameState.SurfaceState.AiObjects.Add(CreateAiObject("KamikazeDrone"));

        _director.Update();

        Assert.IsFalse(_director.IsVictory);
    }

    // -----------------------------------------------------------------
    // Multiple motherships
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_MultipleMotherShips_AllActivated()
    {
        var ms1 = CreateAiObject("MotherShipSmall", isActive: false);
        var ms2 = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(ms1);
        GameState.SurfaceState.AiObjects.Add(ms2);
        GameState.GamePlayState.InitialSeeders = 1;

        _director.Update();

        Assert.IsTrue(ms1.IsActive);
        Assert.IsTrue(ms2.IsActive);
        Assert.AreEqual(2, GameState.GamePlayState.MotherShipsRemaining);
    }
}
