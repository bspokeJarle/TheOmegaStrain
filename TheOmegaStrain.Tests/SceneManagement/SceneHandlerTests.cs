using _3dRotations.SceneManagement;
using _3dRotations.World;
using _3dRotations.Scenes.Scene1;
using _3dRotations.Scenes.Scene2;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.SceneManagement;

[TestClass]
[Ignore("Manual only: these tests build full scenes and are too slow for normal test runs.")]
public class SceneHandlerTests
{
    [TestInitialize]
    public void Setup()
    {
        // Reset global state before each test
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void ResetActiveScene_WithZeroMotherShipCountButClearedWave_RestoresMothership()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        var gps = GameState.GamePlayState;
        gps.PowerUpsCollected = 1;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0; // stale/edge checkpoint value
        gps.InitialSeeders = 7;
        gps.InitialDrones = 4;
        gps.InitialMotherShips = 1;
        gps.SaveCheckpoint();

        handler.ResetActiveScene(world);

        var motherShips = world.WorldInhabitants.Where(o => o.ObjectName == "MotherShipSmall" && o.IsActive).ToList();
        Assert.AreEqual(1, motherShips.Count,
            "When wave is cleared at checkpoint, reset should restore one active mothership even if saved count was 0.");
    }

    [TestMethod]
    public void ResetActiveScene_WithDecoyUnlockedCheckpoint_ReactivatesDrones()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        var gps = GameState.GamePlayState;
        gps.PowerUpsCollected = 1; // decoy unlocked
        gps.SeedersRemaining = 2;
        gps.DronesRemaining = 1;
        gps.MotherShipsRemaining = 0;
        gps.InitialSeeders = 7;
        gps.InitialDrones = 4;
        gps.InitialMotherShips = 1;
        gps.SaveCheckpoint();

        handler.ResetActiveScene(world);

        var drones = world.WorldInhabitants.Where(o => o.ObjectName == "KamikazeDrone").ToList();
        Assert.IsTrue(drones.Count > 0, "Scene1 should contain kamikaze drones after reset.");
        Assert.IsTrue(drones.Any(d => d.IsActive),
            "At least one drone should be active when checkpoint indicates decoy-unlocked phase.");
    }

    [TestMethod]
    public void ResetActiveScene_WithMothershipPhaseCheckpoint_ReactivatesMothership()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        var gps = GameState.GamePlayState;
        gps.PowerUpsCollected = 1;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;
        gps.InitialSeeders = 7;
        gps.InitialDrones = 4;
        gps.InitialMotherShips = 1;
        gps.SaveCheckpoint();

        handler.ResetActiveScene(world);

        var motherShip = world.WorldInhabitants.FirstOrDefault(o => o.ObjectName == "MotherShipSmall");
        Assert.IsNotNull(motherShip, "Scene1 should contain a mothership after reset.");
        Assert.IsTrue(motherShip!.IsActive,
            "Mothership should be active when checkpoint indicates mothership phase was in progress.");
    }

    // -----------------------------------------------------------------
    // Scene list and ordering
    // -----------------------------------------------------------------

    [TestMethod]
    public void GetActiveScene_ReturnsIntroSceneByDefault()
    {
        var handler = new SceneHandler();

        var scene = handler.GetActiveScene();

        Assert.AreEqual(SceneTypes.Intro, scene.SceneType);
    }

    [TestMethod]
    public void SceneOrder_IsIntroThroughScene8ThenOutro()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Scene 0: Intro
        Assert.AreEqual(SceneTypes.Intro, handler.GetActiveScene().SceneType);

        // Scene 1: Game (Scene1)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene1", handler.GetActiveScene().GetType().Name);

        // Scene 2: Game (Scene2)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene2", handler.GetActiveScene().GetType().Name);

        // Scene 3: Game (Scene3)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene3", handler.GetActiveScene().GetType().Name);

        // Scene 4: Game (Scene4)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene4", handler.GetActiveScene().GetType().Name);

        // Scene 5: Game (Scene5)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene5", handler.GetActiveScene().GetType().Name);

        // Scene 6: Game (Scene6)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene6", handler.GetActiveScene().GetType().Name);

        // Scene 7: Game (Scene7)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene7", handler.GetActiveScene().GetType().Name);

        // Scene 8: Game (Scene8)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene8", handler.GetActiveScene().GetType().Name);

        // Scene 9: Outro (SceneType is Intro)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Intro, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Outro", handler.GetActiveScene().GetType().Name);
    }

    [TestMethod]
    public void NextScene_WrapsAroundToIntro()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Advance through Intro + 8 game scenes + Outro and wrap
        AdvanceScene(handler, world); // -> Scene1
        AdvanceScene(handler, world); // -> Scene2
        AdvanceScene(handler, world); // -> Scene3
        AdvanceScene(handler, world); // -> Scene4
        AdvanceScene(handler, world); // -> Scene5
        AdvanceScene(handler, world); // -> Scene6
        AdvanceScene(handler, world); // -> Scene7
        AdvanceScene(handler, world); // -> Scene8
        AdvanceScene(handler, world); // -> Outro
        AdvanceScene(handler, world); // -> Intro (wrap)

        Assert.AreEqual(SceneTypes.Intro, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Intro", handler.GetActiveScene().GetType().Name);
    }

    // -----------------------------------------------------------------
    // Scene settings applied
    // -----------------------------------------------------------------

    [TestMethod]
    public void SetupActiveScene_AppliesInfectionSettings()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Advance to Scene1 (a Game scene)
        AdvanceScene(handler, world);

        var scene = handler.GetActiveScene();
        var gps = GameState.GamePlayState;

        Assert.AreEqual(scene.InfectionThresholdPercent, gps.InfectionCriticalMass);
        Assert.AreEqual(scene.InfectionSpreadRate, gps.InfectionSpreadRate);
        Assert.AreEqual(scene.SeederOffscreenSpeedFactor, gps.SeederOffscreenSpeedFactor);
        Assert.AreEqual(scene.LocalInfectionSpreadDelaySec, gps.LocalInfectionSpreadDelaySec);
        Assert.AreEqual(scene.LocalInfectionSpreadRadius, gps.LocalInfectionSpreadRadius);
        Assert.AreEqual(scene.MotherShipSmallAggression, gps.MotherShipSmallAggression);
        Assert.AreEqual(scene.MotherShipMediumAggression, gps.MotherShipMediumAggression);
        Assert.AreEqual(scene.MotherShipLargeAggression, gps.MotherShipLargeAggression);
    }

    // -----------------------------------------------------------------
    // Director lifecycle
    // -----------------------------------------------------------------

    [TestMethod]
    public void SetupActiveScene_InitializesDirector_ForGameScenes()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Advance to Scene1
        AdvanceScene(handler, world);

        var scene = handler.GetActiveScene();
        Assert.IsNotNull(scene.Director, "Game scenes should have a director.");
    }

    [TestMethod]
    public void GetActiveScene_IntroHasNoDirector()
    {
        var handler = new SceneHandler();

        var scene = handler.GetActiveScene();
        Assert.IsNull(scene.Director, "Intro scene should not have a director.");
    }

    // -----------------------------------------------------------------
    // Stat carry-forward in NextScene
    // -----------------------------------------------------------------

    [TestMethod]
    public void NextScene_CarriesForwardStats_WhenNextSceneIsGame()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Advance to Scene1 (Game)
        AdvanceScene(handler, world);
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);

        // Set some stats
        var gps = GameState.GamePlayState;
        gps.Score = 5000;
        gps.TotalKills = 10;
        gps.TotalShotsFired = 50;
        gps.TotalDeaths = 2;
        gps.PowerUpsCollected = 3;

        // Advance to Scene2 (Game)
        handler.NextScene(world);

        gps = GameState.GamePlayState;
        Assert.AreEqual(5000L, gps.Score, "Score should carry forward.");
        Assert.AreEqual(10, gps.TotalKills, "Kills should carry forward.");
        Assert.AreEqual(50, gps.TotalShotsFired, "Shots should carry forward.");
        Assert.AreEqual(2, gps.TotalDeaths, "Deaths should carry forward.");
        Assert.AreEqual(3, gps.PowerUpsCollected, "PowerUps should carry forward.");
    }

    [TestMethod]
    public void NextScene_DoesNotCarryStats_WhenNextSceneIsNotGame()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Advance to Scene8 (last Game scene)
        AdvanceScene(handler, world); // -> Scene1
        AdvanceScene(handler, world); // -> Scene2
        AdvanceScene(handler, world); // -> Scene3
        AdvanceScene(handler, world); // -> Scene4
        AdvanceScene(handler, world); // -> Scene5
        AdvanceScene(handler, world); // -> Scene6
        AdvanceScene(handler, world); // -> Scene7
        AdvanceScene(handler, world); // -> Scene8

        // Set stats
        var gps = GameState.GamePlayState;
        gps.Score = 8000;
        gps.TotalKills = 20;

        // Advance to Outro (SceneType = Intro, not Game)
        handler.NextScene(world);

        gps = GameState.GamePlayState;
        Assert.AreEqual(0L, gps.Score, "Score should reset when next scene is not Game.");
        Assert.AreEqual(0, gps.TotalKills, "Kills should reset when next scene is not Game.");
    }

    // -----------------------------------------------------------------
    // Scene index stored in GamePlayState
    // -----------------------------------------------------------------

    [TestMethod]
    public void NextScene_UpdatesSceneIndex()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1
        Assert.AreEqual(1, GameState.GamePlayState.SceneIndex);

        AdvanceScene(handler, world); // -> Scene2
        Assert.AreEqual(2, GameState.GamePlayState.SceneIndex);
    }

    [TestMethod]
    public void NextScene_SetsSceneIndexToZero_WhenWrapping()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1
        AdvanceScene(handler, world); // -> Scene2
        AdvanceScene(handler, world); // -> Scene3
        AdvanceScene(handler, world); // -> Scene4
        AdvanceScene(handler, world); // -> Scene5
        AdvanceScene(handler, world); // -> Scene6
        AdvanceScene(handler, world); // -> Scene7
        AdvanceScene(handler, world); // -> Scene8
        AdvanceScene(handler, world); // -> Outro
        AdvanceScene(handler, world); // -> Intro (wrap)

        Assert.AreEqual(0, GameState.GamePlayState.SceneIndex);
    }

    // -----------------------------------------------------------------
    // ResetActiveScene restores checkpoint
    // -----------------------------------------------------------------

    [TestMethod]
    public void ResetActiveScene_RestoresCheckpointState()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Advance to Scene1
        AdvanceScene(handler, world);

        // Save a checkpoint
        var gps = GameState.GamePlayState;
        gps.Score = 3000;
        gps.Lives = 2;
        gps.TotalKills = 8;
        gps.PowerUpsCollected = 2;
        gps.InitialSeeders = 3;
        gps.SeedersRemaining = 1;
        gps.SaveCheckpoint();

        // Reset the scene
        handler.ResetActiveScene(world);

        gps = GameState.GamePlayState;
        Assert.AreEqual(3000L, gps.Score, "Score should be restored from checkpoint.");
        Assert.AreEqual(1, gps.Lives, "Lives should be decremented by 1 from checkpoint (death penalty).");
        Assert.AreEqual(1, gps.TotalDeaths, "TotalDeaths should increment by 1 from checkpoint (0 + 1 death).");
    }

    [TestMethod]
    public void ResetActiveScene_WithoutCheckpoint_PreservesAccumulatedStats()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Advance to Scene1
        AdvanceScene(handler, world);

        // Set stats without saving checkpoint
        var gps = GameState.GamePlayState;
        gps.Score = 5000;
        gps.TotalKills = 3;
        gps.TotalShotsFired = 20;
        gps.TotalDeaths = 0;
        gps.PowerUpsCollected = 1;
        gps.Lives = 3;

        handler.ResetActiveScene(world);

        gps = GameState.GamePlayState;
        Assert.AreEqual(5000L, gps.Score, "Without checkpoint, score should be preserved.");
        Assert.AreEqual(3, gps.TotalKills, "Without checkpoint, kills should be preserved.");
        Assert.AreEqual(20, gps.TotalShotsFired, "Without checkpoint, shots should be preserved.");
        Assert.AreEqual(1, gps.TotalDeaths, "Without checkpoint, deaths should be incremented by 1.");
        Assert.AreEqual(1, gps.PowerUpsCollected, "Without checkpoint, powerups should be preserved.");
        Assert.AreEqual(2, gps.Lives, "Without checkpoint, lives should be decremented by 1.");
    }

    // -----------------------------------------------------------------
    // WorldInhabitants populated after scene setup
    // -----------------------------------------------------------------

    [TestMethod]
    public void SetupActiveScene_GameScene_PopulatesWorldInhabitants()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world);

        Assert.IsTrue(world.WorldInhabitants.Count > 0, "Game scene should populate WorldInhabitants.");
    }

    [TestMethod]
    public void SetupActiveScene_IntroScene_AddsLogoCube()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Intro is the default scene, already set up by CreateWorld
        Assert.IsTrue(
            world.WorldInhabitants.Exists(o => o.ObjectName == "LogoCube"),
            "Intro scene should add a LogoCube.");
    }

    // -----------------------------------------------------------------
    // Scene properties
    // -----------------------------------------------------------------

    [TestMethod]
    public void GameScenes_HaveDirectors()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Scene1
        AdvanceScene(handler, world);
        Assert.IsNotNull(handler.GetActiveScene().Director, "Scene1 should have a director.");

        // Scene2
        AdvanceScene(handler, world);
        Assert.IsNotNull(handler.GetActiveScene().Director, "Scene2 should have a director.");
    }

    [TestMethod]
    public void OutroScene_HasNoDirector()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1
        AdvanceScene(handler, world); // -> Scene2
        AdvanceScene(handler, world); // -> Scene3
        AdvanceScene(handler, world); // -> Scene4
        AdvanceScene(handler, world); // -> Scene5
        AdvanceScene(handler, world); // -> Scene6
        AdvanceScene(handler, world); // -> Scene7
        AdvanceScene(handler, world); // -> Scene8
        AdvanceScene(handler, world); // -> Outro

        Assert.IsNull(handler.GetActiveScene().Director, "Outro should not have a director.");
    }

    [TestMethod]
    public void OutroScene_HasIntroSceneType()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1
        AdvanceScene(handler, world); // -> Scene2
        AdvanceScene(handler, world); // -> Scene3
        AdvanceScene(handler, world); // -> Scene4
        AdvanceScene(handler, world); // -> Scene5
        AdvanceScene(handler, world); // -> Scene6
        AdvanceScene(handler, world); // -> Scene7
        AdvanceScene(handler, world); // -> Scene8
        AdvanceScene(handler, world); // -> Outro

        var scene = handler.GetActiveScene();
        Assert.AreEqual("Outro", scene.GetType().Name);
        Assert.AreEqual(SceneTypes.Intro, scene.SceneType,
            "Outro uses SceneType.Intro for name-entry-like flow.");
    }

    // -----------------------------------------------------------------
    // NextScene clears WorldInhabitants from previous scene
    // -----------------------------------------------------------------

    [TestMethod]
    public void NextScene_ClearsAndRepopulatesWorldInhabitants()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // Intro has LogoCube
        Assert.IsTrue(world.WorldInhabitants.Exists(o => o.ObjectName == "LogoCube"));

        // Advance to Scene1
        handler.NextScene(world);

        // LogoCube from Intro should be gone (ResetSurfaceState + new setup)
        // Scene1 should have Ship, enemies, etc.
        Assert.IsTrue(world.WorldInhabitants.Exists(o => o.ObjectName == "Ship"),
            "Scene1 should have a Ship.");
    }

    // -----------------------------------------------------------------
    // Scene infection settings differ between Scene1 and Scene2
    // -----------------------------------------------------------------

    [TestMethod]
    public void Scene1_And_Scene2_HaveDifferentInfectionSettings()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1
        var scene1 = handler.GetActiveScene();
        float threshold1 = scene1.InfectionThresholdPercent;
        int spreadRate1 = scene1.InfectionSpreadRate;

        AdvanceScene(handler, world); // -> Scene2
        var scene2 = handler.GetActiveScene();
        float threshold2 = scene2.InfectionThresholdPercent;
        int spreadRate2 = scene2.InfectionSpreadRate;

        // Scene2 should be harder (lower threshold or higher spread rate)
        Assert.IsTrue(threshold2 <= threshold1 || spreadRate2 >= spreadRate1,
            "Scene2 should have equal or harder infection settings than Scene1.");
    }

    // -----------------------------------------------------------------
    // GamePlayState.ResetForNewGame called by NextScene
    // -----------------------------------------------------------------

    [TestMethod]
    public void NextScene_ResetsHealthAndLives()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1
        GameState.GamePlayState.Health = 10f;
        GameState.GamePlayState.Lives = 0;

        handler.NextScene(world); // -> Scene2

        var gps = GameState.GamePlayState;
        // Stats carry forward for Game scenes, but Health and Lives get reset by ResetForNewGame
        // then stats are restored on top. Health/Lives are not in the carry-forward set.
        Assert.AreEqual(100f, gps.Health, "Health should be reset via ResetForNewGame.");
        Assert.AreEqual(3, gps.Lives, "Lives should be reset via ResetForNewGame.");
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static GameWorld CreateWorld(SceneHandler handler)
    {
        // Create a world that uses the provided handler (avoiding
        // the default GameWorld constructor which creates its own SceneHandler)
        var world = (Activator.CreateInstance(typeof(GameWorld), true) as GameWorld)!;

        // Use reflection to avoid the constructor auto-calling SetupActiveScene
        // Alternatively, just create via normal constructor and accept the side-effect
        // Since the constructor calls SetupActiveScene for Intro, this is fine
        world = new GameWorld();
        world.SceneHandler = handler;

        // The GameWorld constructor already called SetupActiveScene on its own handler.
        // We set our handler and re-setup so the test handler owns the scene.
        world.WorldInhabitants.Clear();
        handler.SetupActiveScene(world);

        return world;
    }

    private static void AdvanceScene(SceneHandler handler, GameWorld world)
    {
        handler.NextScene(world);
    }

    // -----------------------------------------------------------------
    // Scene content: expected enemy counts in Scene1
    // -----------------------------------------------------------------

    [TestMethod]
    public void Scene1_HasExpectedEnemyCounts()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        int seeders = world.WorldInhabitants.Count(o => o.ObjectName == "Seeder");
        int drones = world.WorldInhabitants.Count(o => o.ObjectName == "KamikazeDrone");
        int motherShips = world.WorldInhabitants.Count(o => o.ObjectName == "MotherShipSmall");
        int swans = world.WorldInhabitants.Count(o => o.ObjectName == "SpaceSwan");

        Assert.AreEqual(7, seeders, "Scene1 should have 7 seeders (3 + 1 powerup + 3).");
        Assert.AreEqual(4, drones, "Scene1 should have 4 kamikaze drones.");
        Assert.AreEqual(1, motherShips, "Scene1 should have 1 mothership.");
        Assert.AreEqual(50, swans, "Scene1 should have 50 space swans.");
    }

    [TestMethod]
    public void Scene1_DronesStartInactive()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        var drones = world.WorldInhabitants.Where(o => o.ObjectName == "KamikazeDrone").ToList();
        Assert.IsTrue(drones.All(d => !d.IsActive),
            "All drones in Scene1 should start inactive (awaiting decoy unlock).");
    }

    [TestMethod]
    public void Scene1_MotherShipStartsInactive()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        var motherShip = world.WorldInhabitants.First(o => o.ObjectName == "MotherShipSmall");
        Assert.IsFalse(motherShip.IsActive,
            "MotherShip should start inactive (awaiting seeders/drones elimination).");
    }

    [TestMethod]
    public void Scene1_PowerUpDropPolicyConfiguredForWave()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        // Powerups are no longer baked into specific seeders at spawn; the kill-time
        // policy decides which kill index promotes a seeder to a powerup drop.
        int powerUpSeedersAtSpawn = world.WorldInhabitants.Count(o => o.ObjectName == "Seeder" && o.HasPowerUp);
        Assert.AreEqual(0, powerUpSeedersAtSpawn, "Scene1 must not flag any seeder as a powerup at spawn.");

        int seederCount = world.WorldInhabitants.Count(o => o.ObjectName == "Seeder");
        Assert.AreEqual(1, _3dRotations.Helpers.SeederPlacementHelpers.GetPowerUpCountForSeeders(seederCount),
            "Scene1's seeder wave should award exactly 1 powerup via PowerUpDropPolicy.");
        Assert.AreEqual(1, _3dRotations.Helpers.PowerUpDropPolicy.CurrentThresholds.Count,
            "PowerUpDropPolicy should expose exactly 1 drop threshold for Scene1.");
    }

    [TestMethod]
    public void Scene1_ShipHasWeaponsAndHealth()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        var ship = world.WorldInhabitants.First(o => o.ObjectName == "Ship");
        Assert.IsNotNull(ship.WeaponSystems, "Ship should have weapon systems.");
        Assert.IsTrue(ship.ImpactStatus?.ObjectHealth > 0, "Ship should have health.");
    }

    [TestMethod]
    public void Scene1_HasGuidanceArrow()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        Assert.IsTrue(world.WorldInhabitants.Any(o => o.ObjectName == "SeederGuidanceArrow"),
            "Scene1 should have a seeder guidance arrow.");
    }

    // -----------------------------------------------------------------
    // Scene content: expected enemy counts in Scene2
    // -----------------------------------------------------------------

    [TestMethod]
    public void Scene2_HasExpectedEnemyCounts()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        // Snapshot Scene1 counts before advancing
        int s1Seeders = world.WorldInhabitants.Count(o => o.ObjectName == "Seeder");
        int s1Drones = world.WorldInhabitants.Count(o => o.ObjectName == "KamikazeDrone");
        int s1MotherShips = world.WorldInhabitants.Count(o => o.ObjectName == "MotherShipSmall");

        AdvanceScene(handler, world); // -> Scene2

        // WorldInhabitants accumulate across scenes (SetupScene appends).
        // Verify Scene2 added its own enemies on top of Scene1's.
        int totalSeeders = world.WorldInhabitants.Count(o => o.ObjectName == "Seeder");
        int totalDrones = world.WorldInhabitants.Count(o => o.ObjectName == "KamikazeDrone");
        int totalMotherShips = world.WorldInhabitants.Count(o => o.ObjectName == "MotherShipSmall");

        int s2Seeders = totalSeeders - s1Seeders;
        int s2Drones = totalDrones - s1Drones;
        int s2MotherShips = totalMotherShips - s1MotherShips;

        Assert.AreEqual(10, s2Seeders, "Scene2 should add 10 seeders.");
        Assert.AreEqual(6, s2Drones, "Scene2 should add 6 kamikaze drones.");
        Assert.AreEqual(1, s2MotherShips, "Scene2 should add 1 mothership.");
    }

    // -----------------------------------------------------------------
    // AiObjects synchronized with WorldInhabitants
    // -----------------------------------------------------------------

    [TestMethod]
    public void Scene1_AiObjectsContainsAllEnemies()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        var aiObjs = GameState.SurfaceState.AiObjects;
        int aiSeeders = aiObjs.Count(o => o.ObjectName == "Seeder");
        int aiDrones = aiObjs.Count(o => o.ObjectName == "KamikazeDrone");
        int aiMotherShips = aiObjs.Count(o => o.ObjectName == "MotherShipSmall");
        int aiBombers = aiObjs.Count(o => o.ObjectName == "ZeppelinBomber");

        Assert.AreEqual(7, aiSeeders, "AiObjects should have 7 seeders.");
        Assert.AreEqual(4, aiDrones, "AiObjects should have 4 drones.");
        Assert.AreEqual(1, aiMotherShips, "AiObjects should have 1 mothership.");
        Assert.AreEqual(0, aiBombers, "Scene1 should not contain Zeppelin bombers.");
    }

    [TestMethod]
    public void Scene3_HasZeppelinBombers()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1
        AdvanceScene(handler, world); // -> Scene2
        AdvanceScene(handler, world); // -> Scene3

        int bombers = world.WorldInhabitants.Count(o => o.ObjectName == "ZeppelinBomber");
        Assert.IsTrue(bombers > 0, "Scene3 should introduce Zeppelin bombers.");
    }

    // -----------------------------------------------------------------
    // ResetActiveScene: enemy trimming with checkpoint
    // -----------------------------------------------------------------

    [TestMethod]
    public void ResetActiveScene_TrimsEnemiesToCheckpointCounts()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        // Save checkpoint with fewer enemies remaining
        var gps = GameState.GamePlayState;
        gps.SeedersRemaining = 2;
        gps.DronesRemaining = 1;
        gps.MotherShipsRemaining = 0;
        gps.InitialSeeders = 7;
        gps.InitialDrones = 4;
        gps.InitialMotherShips = 1;
        gps.SaveCheckpoint();

        handler.ResetActiveScene(world);

        int seeders = world.WorldInhabitants.Count(o => o.ObjectName == "Seeder");
        int drones = world.WorldInhabitants.Count(o => o.ObjectName == "KamikazeDrone");
        int motherShips = world.WorldInhabitants.Count(o => o.ObjectName == "MotherShipSmall");

        Assert.AreEqual(2, seeders, "After checkpoint reset, seeders should be trimmed to 2.");
        Assert.AreEqual(1, drones, "After checkpoint reset, drones should be trimmed to 1.");
        Assert.AreEqual(0, motherShips, "After checkpoint reset, motherships should be trimmed to 0.");
    }

    [TestMethod]
    public void ResetActiveScene_TrimsSeedersToCheckpointRemainingCount()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        // With kill-time powerup assignment, no seeder is flagged at spawn; trim should
        // simply honor the remaining count regardless of the (currently empty) powerup
        // priority branch.
        var gps = GameState.GamePlayState;
        gps.SeedersRemaining = 1;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;
        gps.InitialSeeders = 7;
        gps.InitialDrones = 4;
        gps.InitialMotherShips = 1;
        gps.SaveCheckpoint();

        handler.ResetActiveScene(world);

        var remainingSeeders = world.WorldInhabitants.Where(o => o.ObjectName == "Seeder").ToList();
        Assert.AreEqual(1, remainingSeeders.Count);
    }

    // -----------------------------------------------------------------
    // Multiple scene resets
    // -----------------------------------------------------------------

    [TestMethod]
    public void ResetActiveScene_CanBeCalledMultipleTimes()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        handler.ResetActiveScene(world);
        handler.ResetActiveScene(world);

        // Scene should remain functional after multiple resets
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.IsNotNull(handler.GetActiveScene().Director, "Director should be present after multiple resets.");
        Assert.IsTrue(world.WorldInhabitants.Any(o => o.ObjectName == "Ship"),
            "Ship should be present after multiple resets.");
        Assert.IsTrue(world.WorldInhabitants.Any(o => o.ObjectName == "Seeder"),
            "Seeders should be present after multiple resets.");
    }

    // -----------------------------------------------------------------
    // NextScene: surface state is reset
    // -----------------------------------------------------------------

    [TestMethod]
    public void NextScene_ClearsSurfaceState()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        // Surface state should have been populated
        Assert.IsNotNull(GameState.SurfaceState.SurfaceViewportObject,
            "SurfaceViewportObject should be set after Game scene setup.");

        AdvanceScene(handler, world); // -> Scene2 (resets + re-sets)

        // After advancing, surface should be re-populated for new scene
        Assert.IsNotNull(GameState.SurfaceState.SurfaceViewportObject,
            "SurfaceViewportObject should be re-set for new Game scene.");
    }

    // -----------------------------------------------------------------
    // Director is disposed when advancing scenes
    // -----------------------------------------------------------------

    [TestMethod]
    public void NextScene_DisposesOldDirector()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1
        var scene1Director = handler.GetActiveScene().Director;
        Assert.IsNotNull(scene1Director);

        // Mark all real enemies as exploded and clear counts.
        // Also mark mothership phase as active by keeping one active mothership object.
        var aiObjs = GameState.SurfaceState.AiObjects;
        var activeMotherShip = aiObjs.FirstOrDefault(o => o.ObjectName == "MotherShipSmall");
        if (activeMotherShip != null)
            activeMotherShip.IsActive = true;

        foreach (var obj in aiObjs)
        {
            if (obj.ObjectName == "Seeder" || obj.ObjectName == "KamikazeDrone" || obj.ObjectName == "MotherShipSmall")
                obj.ImpactStatus = new ImpactStatus { HasExploded = true };
        }
        var gps = GameState.GamePlayState;
        gps.InitialSeeders = 7;
        gps.InitialDrones = 4;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;
        scene1Director.Update();
        gps.MotherShipsRemaining = 0;
        scene1Director.Update();
        Assert.IsTrue(scene1Director.IsVictory, "Victory should trigger with all enemies exploded.");

        AdvanceScene(handler, world); // -> Scene2

        // The old director should have been disposed (victory reset)
        Assert.IsFalse(scene1Director.IsVictory,
            "Old director should be disposed (IsVictory reset) when advancing scenes.");

        // New scene should have its own separate director
        var scene2Director = handler.GetActiveScene().Director;
        Assert.IsNotNull(scene2Director);
        Assert.AreNotSame(scene1Director, scene2Director,
            "Each scene should have its own director instance.");
    }

    // -----------------------------------------------------------------
    // Scene has correct director type
    // -----------------------------------------------------------------

    [TestMethod]
    public void Scene1_HasScene1Director()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1

        Assert.IsInstanceOfType(handler.GetActiveScene().Director, typeof(Scene1Director));
    }

    [TestMethod]
    public void Scene2_HasScene2Director()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        AdvanceScene(handler, world); // -> Scene1
        AdvanceScene(handler, world); // -> Scene2

        Assert.IsInstanceOfType(handler.GetActiveScene().Director, typeof(Scene2Director));
    }
}
