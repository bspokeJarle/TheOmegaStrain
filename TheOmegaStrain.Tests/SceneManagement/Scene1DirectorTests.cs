using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Scenes.Scene1;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class Scene1DirectorTests
{
    private Scene1Director _director = null!;
    private GameEventBus _eventBus = null!;
    private TestWorld _world = null!;

    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();

        _director = new Scene1Director();
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

        Assert.IsFalse(drone.IsActive, "Drones should remain inactive when decoy is not unlocked.");
    }

    [TestMethod]
    public void Update_ActivatesDrones_WhenDecoyUnlocked()
    {
        var drone1 = CreateAiObject("KamikazeDrone", isActive: false);
        var drone2 = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(drone1);
        GameState.SurfaceState.AiObjects.Add(drone2);

        // Unlock decoy (1 powerup collected)
        GameState.GamePlayState.PowerUpsCollected = 1;

        _director.Update();

        Assert.IsTrue(drone1.IsActive, "Drone 1 should be activated.");
        Assert.IsTrue(drone2.IsActive, "Drone 2 should be activated.");
    }

    [TestMethod]
    public void Update_ActivatesDronesOnlyOnce()
    {
        var drone = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(drone);
        GameState.GamePlayState.PowerUpsCollected = 1;

        _director.Update();
        Assert.IsTrue(drone.IsActive);

        // Add a new inactive drone after first activation
        var lateDrone = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(lateDrone);

        _director.Update();

        Assert.IsFalse(lateDrone.IsActive, "Late drone should not be activated; drone activation already happened.");
    }

    [TestMethod]
    public void Update_DoesNotActivateNonDroneObjects_WhenDecoyUnlocked()
    {
        var seeder = CreateAiObject("Seeder", isActive: false);
        var drone = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(seeder);
        GameState.SurfaceState.AiObjects.Add(drone);
        GameState.GamePlayState.PowerUpsCollected = 1;

        _director.Update();

        Assert.IsFalse(seeder.IsActive, "Seeder should not be affected by drone activation.");
        Assert.IsTrue(drone.IsActive);
    }

    // -----------------------------------------------------------------
    // Mothership activation phase
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_DoesNotActivateMotherShip_WhenSeedersRemain()
    {
        var seeder = CreateAiObject("Seeder");
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(seeder);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 1;

        _director.Update();

        Assert.IsFalse(motherShip.IsActive, "MotherShip should not activate while seeders are alive.");
    }

    [TestMethod]
    public void Update_DoesNotActivateMotherShip_WhenDronesRemain()
    {
        var drone = CreateAiObject("KamikazeDrone");
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(drone);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 1;

        _director.Update();

        Assert.IsFalse(motherShip.IsActive, "MotherShip should not activate while drones are alive.");
    }

    [TestMethod]
    public void Update_ActivatesMotherShip_WhenAllSeedersAndDronesEliminated()
    {
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 3;

        // No seeders or drones in AiObjects = all eliminated
        _director.Update();

        Assert.IsTrue(motherShip.IsActive, "MotherShip should be activated when all seeders and drones are eliminated.");
    }

    [TestMethod]
    public void Update_MotherShipActivation_SavesCheckpoint()
    {
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 3;
        GameState.GamePlayState.Score = 500;

        _director.Update();

        Assert.IsTrue(GameState.GamePlayState.HasCheckpoint, "Checkpoint should be saved when mothership activates.");
        Assert.AreEqual(500L, GameState.GamePlayState.CheckpointScore);
    }

    [TestMethod]
    public void Update_MotherShipActivation_SetsRemainingCounts()
    {
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(motherShip);
        GameState.GamePlayState.InitialSeeders = 3;

        _director.Update();

        var gps = GameState.GamePlayState;
        Assert.AreEqual(0, gps.SeedersRemaining);
        Assert.AreEqual(0, gps.DronesRemaining);
        Assert.AreEqual(1, gps.MotherShipsRemaining);
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
    public void Update_SetsVictory_WhenAllEnemyCountsZeroAndNoLiveEnemiesInAiObjects()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 3;
        gps.InitialDrones = 2;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;

        var activeMotherShip = CreateAiObject("MotherShipSmall", isActive: true);
        GameState.SurfaceState.AiObjects.Add(activeMotherShip);

        // AiObjects has only non-enemy objects or exploded enemies
        var explodedSeeder = CreateAiObject("Seeder");
        explodedSeeder.ImpactStatus = new ImpactStatus { HasExploded = true };
        GameState.SurfaceState.AiObjects.Add(explodedSeeder);
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("Tree"));

        _director.Update();
        activeMotherShip.ImpactStatus = new ImpactStatus { HasExploded = true };
        gps.MotherShipsRemaining = 0;
        _director.Update();

        Assert.IsTrue(_director.IsVictory);
    }

    [TestMethod]
    public void Update_DoesNotSetVictory_WhenLiveSeederRemainsInAiObjects()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 3;
        gps.InitialDrones = 2;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        // A live (non-exploded) seeder still in AiObjects
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("Seeder"));

        _director.Update();

        Assert.IsFalse(_director.IsVictory, "Victory should not trigger while live enemies remain in AiObjects.");
    }

    [TestMethod]
    public void Update_DoesNotSetVictory_WhenDronesRemainingIsNonZero()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 3;
        gps.InitialDrones = 2;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 1;
        gps.MotherShipsRemaining = 0;

        _director.Update();

        Assert.IsFalse(_director.IsVictory);
    }

    [TestMethod]
    public void Update_DoesNotSetVictory_WhenMotherShipsRemainingIsNonZero()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 3;
        gps.InitialDrones = 2;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;

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

        Assert.IsFalse(_director.IsVictory, "Victory should not trigger when initial counts are 0 (scene not initialized).");
    }

    [TestMethod]
    public void Update_SetsVictory_WhenRestoredMotherShipPhaseHasMissingInitialCounts()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 0;
        gps.InitialDrones = 0;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;

        var activeMotherShip = CreateAiObject("MotherShipSmall", isActive: true);
        GameState.SurfaceState.AiObjects.Add(activeMotherShip);

        _director.Dispose();
        _director.Initialize(_eventBus, _world);

        GameState.SurfaceState.AiObjects.Remove(activeMotherShip);
        gps.MotherShipsRemaining = 0;

        _director.Update();

        Assert.IsTrue(_director.IsVictory,
            "A restored mothership phase should finish after cleanup removes the last mothership, even if legacy saves missed initial counts.");
    }

    [TestMethod]
    public void Update_DoesNotSetVictory_BeforeMothershipPhaseBegins()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 3;
        gps.InitialDrones = 2;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        _director.Update();

        Assert.IsFalse(_director.IsVictory,
            "Victory must not trigger before mothership phase has started.");
    }

    [TestMethod]
    public void Update_SetsVictory_WhenMothershipPhaseAlreadyActiveAndNoEnemiesRemain()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 3;
        gps.InitialDrones = 2;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        var activeMotherShip = CreateAiObject("MotherShipSmall", isActive: true);
        GameState.SurfaceState.AiObjects.Add(activeMotherShip);

        _director.Update();
        activeMotherShip.ImpactStatus = new ImpactStatus { HasExploded = true };
        gps.MotherShipsRemaining = 0;
        _director.Update();

        Assert.IsTrue(_director.IsVictory,
            "Victory should trigger after mothership phase has started and all enemies are gone.");
    }

    // -----------------------------------------------------------------
    // Update short-circuit when already resolved
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_SkipsPhaseChecks_WhenAlreadyVictory()
    {
        // Achieve victory after mothership phase starts
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

        // Now add a live enemy — Update should short-circuit, victory stays
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("Seeder"));
        gps.SeedersRemaining = 1;

        _director.Update();

        Assert.IsTrue(_director.IsVictory, "Victory should persist once set, even if new enemies appear.");
    }

    // -----------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------

    [TestMethod]
    public void Dispose_ResetsState()
    {
        // Achieve victory first (after mothership phase starts)
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

        Assert.IsFalse(_director.IsVictory, "Dispose should reset IsVictory.");
        Assert.IsFalse(_director.IsDefeat, "Dispose should reset IsDefeat.");
    }

    // -----------------------------------------------------------------
    // Full phase progression: drones -> mothership -> victory
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_FullPhaseProgression()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 2;
        gps.InitialDrones = 1;

        // Setup: 2 seeders, 1 drone (inactive), 1 mothership (inactive)
        var seeder1 = CreateAiObject("Seeder");
        var seeder2 = CreateAiObject("Seeder");
        var drone = CreateAiObject("KamikazeDrone", isActive: false);
        var motherShip = CreateAiObject("MotherShipSmall", isActive: false);
        var aiObjs = GameState.SurfaceState.AiObjects;
        aiObjs.Add(seeder1);
        aiObjs.Add(seeder2);
        aiObjs.Add(drone);
        aiObjs.Add(motherShip);

        // Phase 1: drones not yet activated (decoy locked)
        _director.Update();
        Assert.IsFalse(drone.IsActive, "Phase 1: drone should stay inactive.");
        Assert.IsFalse(motherShip.IsActive);
        Assert.IsFalse(_director.IsVictory);

        // Phase 2: unlock decoy -> drones activate
        gps.PowerUpsCollected = 1;
        _director.Update();
        Assert.IsTrue(drone.IsActive, "Phase 2: drone should be activated.");
        Assert.IsFalse(motherShip.IsActive, "Phase 2: mothership should stay inactive.");

        // Phase 3: eliminate seeders and drones -> mothership activates
        aiObjs.Remove(seeder1);
        aiObjs.Remove(seeder2);
        aiObjs.Remove(drone);
        _director.Update();
        Assert.IsTrue(motherShip.IsActive, "Phase 3: mothership should be activated.");
        Assert.IsFalse(_director.IsVictory, "Phase 3: victory should not yet trigger.");

        // Phase 4: eliminate mothership -> victory
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;
        motherShip.ImpactStatus = new ImpactStatus { HasExploded = true };
        _director.Update();
        Assert.IsTrue(_director.IsVictory, "Phase 4: victory should trigger.");
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static int _nextObjectId = 1;

    private static OmegaObject3D CreateAiObject(string name, bool isActive = true)
    {
        return new OmegaObject3D
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
    // Re-initialization after Dispose (scene reset round-trip)
    // -----------------------------------------------------------------

    [TestMethod]
    public void Initialize_AfterDispose_ResetsPhaseFlags()
    {
        // Run through to mothership activation
        GameState.GamePlayState.InitialSeeders = 1;
        GameState.GamePlayState.PowerUpsCollected = 1;
        var ms = CreateAiObject("MotherShipSmall", isActive: false);
        GameState.SurfaceState.AiObjects.Add(ms);
        _director.Update();
        Assert.IsTrue(ms.IsActive, "Mothership should have activated.");

        // Dispose and re-initialize
        _director.Dispose();
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        _director.Initialize(_eventBus, _world);

        // Drone activation should be possible again
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
        GameState.GamePlayState.InitialSeeders = 2;
        GameState.GamePlayState.Score = 500;

        _director.Update();
        Assert.IsTrue(motherShip.IsActive);
        Assert.AreEqual(500L, GameState.GamePlayState.CheckpointScore);

        // Change score and call Update again — checkpoint should NOT be overwritten
        GameState.GamePlayState.Score = 9999;
        _director.Update();
        Assert.AreEqual(500L, GameState.GamePlayState.CheckpointScore,
            "Checkpoint should not be overwritten by subsequent Updates.");
    }

    // -----------------------------------------------------------------
    // Victory with mixed exploded + live enemies
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_Victory_ExplodedDronesAndMotherShipDoNotBlock()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 2;
        gps.InitialDrones = 1;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        // Exploded enemies of every combat type
        var exSeeder = CreateAiObject("Seeder");
        exSeeder.ImpactStatus = new ImpactStatus { HasExploded = true };
        var exDrone = CreateAiObject("KamikazeDrone");
        exDrone.ImpactStatus = new ImpactStatus { HasExploded = true };
        var exMs = CreateAiObject("MotherShipSmall");
        exMs.ImpactStatus = new ImpactStatus { HasExploded = true };
        exMs.IsActive = true;
        GameState.SurfaceState.AiObjects.Add(exSeeder);
        GameState.SurfaceState.AiObjects.Add(exDrone);
        GameState.SurfaceState.AiObjects.Add(exMs);

        _director.Update();
        gps.MotherShipsRemaining = 0;

        _director.Update();

        Assert.IsTrue(_director.IsVictory,
            "Exploded enemies should not block victory.");
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

        // Non-combat objects in AiObjects
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("Tree"));
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("SpaceSwan"));
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("Tower"));

        _director.Update();

        motherShip.ImpactStatus = new ImpactStatus { HasExploded = true };
        gps.MotherShipsRemaining = 0;
        _director.Update();

        Assert.IsTrue(_director.IsVictory,
            "Non-enemy objects should not block victory.");
    }

    // -----------------------------------------------------------------
    // Simultaneous conditions — all phases met on first Update
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_AllConditionsMetSimultaneously_ReachesVictory()
    {
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 1;
        gps.InitialDrones = 1;
        gps.PowerUpsCollected = 1; // decoy unlocked

        // No live seeders, no drones, mothership already active and exploded
        var ms = CreateAiObject("MotherShipSmall", isActive: true);
        ms.ImpactStatus = new ImpactStatus { HasExploded = true };
        GameState.SurfaceState.AiObjects.Add(ms);

        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        // Single Update should process all phases and reach victory
        _director.Update();

        Assert.IsTrue(_director.IsVictory,
            "When all conditions met simultaneously, victory should trigger in a single Update.");
    }

    // -----------------------------------------------------------------
    // Empty AiObjects resilience
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_EmptyAiObjects_DoesNotCrash()
    {
        GameState.SurfaceState.AiObjects.Clear();
        GameState.GamePlayState.InitialSeeders = 0;

        // Should not throw
        _director.Update();

        Assert.IsFalse(_director.IsVictory);
        Assert.IsFalse(_director.IsDefeat);
    }

    // -----------------------------------------------------------------
    // Live KamikazeDrone blocks victory even with counts at zero
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

        // Counts say zero, but a live drone is still in AiObjects
        GameState.SurfaceState.AiObjects.Add(CreateAiObject("KamikazeDrone"));

        _director.Update();

        Assert.IsFalse(_director.IsVictory,
            "Live KamikazeDrone in AiObjects should block victory even when remaining counts are 0.");
    }

    // -----------------------------------------------------------------
    // Drone activation skips already-active drones
    // -----------------------------------------------------------------

    [TestMethod]
    public void Update_DroneActivation_SkipsAlreadyActiveDrones()
    {
        var activeDrone = CreateAiObject("KamikazeDrone", isActive: true);
        var inactiveDrone = CreateAiObject("KamikazeDrone", isActive: false);
        GameState.SurfaceState.AiObjects.Add(activeDrone);
        GameState.SurfaceState.AiObjects.Add(inactiveDrone);
        GameState.GamePlayState.PowerUpsCollected = 1;

        _director.Update();

        Assert.IsTrue(activeDrone.IsActive, "Already-active drone should remain active.");
        Assert.IsTrue(inactiveDrone.IsActive, "Inactive drone should be activated.");
    }

    // -----------------------------------------------------------------
    // Multiple motherships: all get activated
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

        Assert.IsTrue(ms1.IsActive, "First mothership should activate.");
        Assert.IsTrue(ms2.IsActive, "Second mothership should activate.");
        Assert.AreEqual(2, GameState.GamePlayState.MotherShipsRemaining,
            "MotherShipsRemaining should reflect all activated motherships.");
    }
}
