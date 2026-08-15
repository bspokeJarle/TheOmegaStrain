using TheOmegaStrain.Game.SceneManagement;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Persistence;
using TheOmegaStrain.Domain;
using System;
using System.IO;
using System.Reflection;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class SceneHandlerSavedStateIsolationTests
{
    private string _originalLocalFolder = string.Empty;
    private string _testLocalFolder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainSceneTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.Initialize();

        GameState.GamePlayState = new GamePlayState { SceneIndex = 0 };
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ObjectIdCounter = 0;

        TutorialProgressService.MarkTutorialCompleted("Jarle");
        TutorialProgressService.MarkTutorialCompleted("Anna");
        TutorialProgressService.MarkTutorialCompleted("CharlieB");
        TutorialProgressService.MarkTutorialCompleted("Pilot");
    }

    [TestCleanup]
    public void Cleanup()
    {
        PersistenceSetup.LocalFolder = _originalLocalFolder;
        try
        {
            if (Directory.Exists(_testLocalFolder))
                Directory.Delete(_testLocalFolder, recursive: true);
        }
        catch
        {
        }
    }

    [TestMethod]
    public void ReturningPlayer_LoadsOnlyOwnSavedSceneIndex()
    {
        // Save Jarle at Scene4
        GameState.GamePlayState = new GamePlayState
        {
            PlayerName = "Jarle",
            SceneIndex = 4,
            Score = 1000
        };
        GameStatePersistence.SaveGameState();

        // Save Anna at Scene1
        GameState.GamePlayState = new GamePlayState
        {
            PlayerName = "Anna",
            SceneIndex = 1,
            Score = 2000
        };
        GameStatePersistence.SaveGameState();

        // Simulate login flow for Anna
        var loadedAnna = GameStatePersistence.LoadGameState("Anna");
        Assert.IsNotNull(loadedAnna);

        // SceneHandler logic should only target Anna's scene index
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        // emulate what HandleNameEntryKey would set for Anna
        GameState.GamePlayState.PlayerName = "Anna";
        GameState.GamePlayState.SceneIndex = 0;

        // We cannot call private handler methods directly; verify persistence source is correct.
        Assert.AreEqual(1, loadedAnna!.SceneIndex);

        var loadedJarle = GameStatePersistence.LoadGameState("Jarle");
        Assert.IsNotNull(loadedJarle);
        Assert.AreEqual(4, loadedJarle!.SceneIndex);

        // Ensure world/handler still starts from intro and can progress deterministically.
        Assert.AreEqual("Intro", handler.GetActiveScene().GetType().Name);
        handler.NextScene(world);
        Assert.AreEqual("Scene1", handler.GetActiveScene().GetType().Name);
    }

    [TestMethod]
    public void NextScene_DoesNotPersistPreviousSceneCheckpointUnderNewSceneIndex()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);
        handler.NextScene(world); // Scene1

        var gps = GameState.GamePlayState;
        gps.PlayerName = "Jarle";
        gps.SceneIndex = 1;
        gps.Score = 1234;
        gps.PowerUpsCollected = 2;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 1;
        gps.InitialSeeders = 7;
        gps.InitialDrones = 4;
        gps.InitialMotherShips = 1;
        gps.SaveCheckpoint();

        handler.NextScene(world);

        var saved = GameStatePersistence.LoadGameState("Jarle");

        Assert.IsNotNull(saved);
        Assert.AreEqual(2, saved!.SceneIndex);
        Assert.IsFalse(saved.HasCheckpoint,
            "Entering a new scene should save fresh scene progress, not the previous scene's checkpoint.");
        Assert.AreEqual(1234, saved.Score);
        Assert.AreEqual(2, saved.PowerUpsCollected);
    }

    [TestMethod]
    public void NextScene_FromIntro_DoesNotCreateSave()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        GameState.GamePlayState.PlayerName = "Jarle";

        handler.NextScene(world);

        Assert.IsFalse(PersistenceSetup.HasPlayerSaveFile("Jarle"),
            "Starting from Intro should not create durable progress; scene completion owns this save.");
        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
    }

    [TestMethod]
    public void NextScene_FromFinalGameScene_SavesOutroAsNextScene()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        SetPrivateField(handler, "currentSceneIndex", 8);
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Jarle";
        gps.SceneIndex = 8;
        gps.Score = 8000;
        gps.TotalKills = 44;

        handler.NextScene(world);

        var saved = GameStatePersistence.LoadGameState("Jarle");
        Assert.IsNotNull(saved);
        Assert.AreEqual(SceneTypes.Outro, handler.GetActiveScene().SceneType);
        Assert.AreEqual(9, saved!.SceneIndex,
            "Completing the final game scene should resume at the Outro instead of replaying Scene8.");
        Assert.IsFalse(saved.HasCheckpoint);
        Assert.AreEqual(0, saved.Score,
            "Outro keeps the existing non-game stat reset behavior; this test only locks the resume scene.");
    }

    [TestMethod]
    public void CanTargetSavedScene_AllowsOutroProgress()
    {
        var handler = new SceneHandler();
        var saved = new SavedGameState
        {
            PlayerName = "Jarle",
            SceneIndex = 9
        };

        var method = typeof(SceneHandler).GetMethod("CanTargetSavedScene", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        var result = (bool)method!.Invoke(handler, new object[] { saved })!;
        Assert.IsTrue(result, "Saved Outro progress should be restorable after completing the final game scene.");
    }

    [TestMethod]
    public void ResetActiveScene_CheckpointWithZeroMotherShips_PreservesSceneMotherShipCandidate()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        handler.NextScene(world); // Scene1
        handler.NextScene(world); // Scene2
        handler.NextScene(world); // Scene3

        var gps = GameState.GamePlayState;
        gps.PowerUpsCollected = 1;
        gps.SeedersRemaining = 5;
        gps.DronesRemaining = 3;
        gps.MotherShipsRemaining = 0;
        gps.InitialSeeders = 12;
        gps.InitialDrones = 8;
        gps.InitialMotherShips = 1;
        gps.SaveCheckpoint();

        handler.ResetActiveScene(world);

        int motherShipCount = GameState.SurfaceState.AiObjects.Count(o => o.ObjectName == "MotherShipSmall");
        Assert.IsTrue(motherShipCount > 0,
            "Checkpoint restore should keep at least one Scene3 mothership candidate even when checkpoint mother ship count is zero.");
    }

    [TestMethod]
    public void UpdateFrame_LoadedCheckpointWithZeroMotherShips_PreservesSceneMotherShipCandidate()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        var loaded = new SavedGameState
        {
            PlayerName = "Jarle",
            SceneIndex = 3,
            Score = 3456,
            TotalKills = 22,
            TotalShotsFired = 100,
            TotalDeaths = 1,
            PowerUpsCollected = 1,
            HasCheckpoint = true,
            CheckpointSeedersRemaining = 5,
            CheckpointDronesRemaining = 3,
            CheckpointMotherShipsRemaining = 0,
            CheckpointInitialSeeders = 12,
            CheckpointInitialDrones = 8,
            CheckpointInitialMotherShips = 1,
            CheckpointTotalKills = 10
        };

        SetPrivateField(handler, "_pendingSavedState", loaded);
        SetPrivateField(handler, "_targetSceneIndex", 3);
        SetPrivateField(handler, "_pendingSceneAdvance", true);
        SetPrivateField(handler, "_pendingSceneAdvanceFramesLeft", 0);

        handler.UpdateFrame(world);

        int motherShipCount = GameState.SurfaceState.AiObjects.Count(o => o.ObjectName == "MotherShipSmall");
        Assert.AreEqual(1, motherShipCount,
            "Loaded checkpoint restore should keep Scene3 mothership candidate even when checkpoint mother ship count is zero.");
    }

    [TestMethod]
    public void UpdateFrame_LoadedSceneBoundarySave_RestoresArrivalSnapshot()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        var loaded = new SavedGameState
        {
            PlayerName = "CharlieB",
            SceneIndex = 1,
            Score = 7200,
            Lives = 2,
            Health = 64f,
            TotalKills = 18,
            TotalShotsFired = 90,
            TotalDeaths = 2,
            PowerUpsCollected = 2,
            HasCheckpoint = false,
            HasPlanetStartSnapshot = true,
            PlanetStartSceneIndex = 1,
            PlanetStartScore = 5000,
            PlanetStartLives = 3,
            PlanetStartHealth = 92f,
            PlanetStartPowerUpsCollected = 1,
            PlanetStartSeedersRemaining = 7,
            PlanetStartDronesRemaining = 4,
            PlanetStartMotherShipsRemaining = 1,
            PlanetStartTotalKills = 12,
            PlanetStartTotalShotsFired = 55,
            PlanetStartTotalDeaths = 1,
            PlanetStartInitialSeeders = 7,
            PlanetStartInitialDrones = 4,
            PlanetStartInitialMotherShips = 1
        };

        SetPrivateField(handler, "_pendingSavedState", loaded);
        SetPrivateField(handler, "_targetSceneIndex", 1);
        SetPrivateField(handler, "_pendingSceneAdvance", true);
        SetPrivateField(handler, "_pendingSceneAdvanceFramesLeft", 0);

        handler.UpdateFrame(world);

        var gps = GameState.GamePlayState;
        Assert.AreEqual(7200L, gps.Score);
        Assert.IsTrue(gps.HasPlanetStartSnapshot);
        Assert.AreEqual(1, gps.PlanetStartSceneIndex);
        Assert.AreEqual(5000L, gps.PlanetStartScore);
        Assert.AreEqual(92f, gps.PlanetStartHealth);
        Assert.AreEqual(7, gps.PlanetStartSeedersRemaining);
        Assert.AreEqual(12, gps.PlanetStartTotalKills);
    }

    [TestMethod]
    public void ResetActiveSceneToPlanetStart_WithoutSnapshot_DiscardsVolatileProgress()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);
        handler.NextScene(world);

        var gps = GameState.GamePlayState;
        gps.PlayerName = "CharlieB";
        gps.Score = 7200;
        gps.Lives = 1;
        gps.Health = 24f;
        gps.TotalKills = 18;
        gps.TotalShotsFired = 90;
        gps.TotalDeaths = 2;
        gps.PowerUpsCollected = 2;
        gps.SpeedPowerUpLevel = 1;
        gps.InfectionLevel = 99f;
        gps.ClearPlanetStartSnapshot();

        handler.ResetActiveSceneToPlanetStart(world);

        Assert.AreEqual(0L, gps.Score);
        Assert.AreEqual(3, gps.Lives);
        Assert.AreEqual(100f, gps.Health);
        Assert.AreEqual(0, gps.TotalKills);
        Assert.AreEqual(0, gps.TotalShotsFired);
        Assert.AreEqual(0, gps.TotalDeaths);
        Assert.AreEqual(0f, gps.InfectionLevel);
        Assert.AreEqual(2, gps.PowerUpsCollected);
        Assert.AreEqual(1, gps.SpeedPowerUpLevel);
        Assert.IsTrue(gps.HasPlanetStartSnapshot);
    }

    [TestMethod]
    public void UpdateFrame_LoadedScene1MothershipCheckpoint_RestoresEvenWhenKillCountIsLowerThanCurrentEnemyTotal()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        var loaded = new SavedGameState
        {
            PlayerName = "CharlieB",
            SceneIndex = 1,
            Score = 900,
            TotalKills = 8,
            TotalShotsFired = 14,
            TotalDeaths = 1,
            PowerUpsCollected = 1,
            HasCheckpoint = true,
            CheckpointScore = 900,
            CheckpointLives = 3,
            CheckpointHealth = 100,
            CheckpointPowerUpsCollected = 1,
            CheckpointSeedersRemaining = 0,
            CheckpointDronesRemaining = 0,
            CheckpointMotherShipsRemaining = 1,
            CheckpointInitialSeeders = 7,
            CheckpointInitialDrones = 4,
            CheckpointInitialMotherShips = 1,
            CheckpointTotalKills = 8,
            CheckpointTotalShotsFired = 14,
            CheckpointTotalDeaths = 1
        };

        SetPrivateField(handler, "_pendingSavedState", loaded);
        SetPrivateField(handler, "_targetSceneIndex", 1);
        SetPrivateField(handler, "_pendingSceneAdvance", true);
        SetPrivateField(handler, "_pendingSceneAdvanceFramesLeft", 0);

        handler.UpdateFrame(world);

        var gps = GameState.GamePlayState;
        Assert.IsTrue(gps.HasCheckpoint, "A saved mothership-phase checkpoint should not be rejected only because kill counters differ.");
        Assert.AreEqual(0, gps.SeedersRemaining);
        Assert.AreEqual(0, gps.DronesRemaining);
        Assert.AreEqual(1, gps.MotherShipsRemaining);
        Assert.AreEqual(1, GameState.SurfaceState.AiObjects.Count(o => o.ObjectName == "MotherShipSmall" && o.IsActive));
    }

    [TestMethod]
    public void UpdateFrame_MismatchedCheckpoint_DoesNotOverwritePersistedSave()
    {
        var handler = new SceneHandler();
        var world = CreateWorld(handler);

        GameState.GamePlayState = new GamePlayState
        {
            PlayerName = "CharlieB",
            SceneIndex = 1,
            Score = 900,
            HasCheckpoint = true,
            CheckpointInitialSeeders = 999,
            CheckpointInitialDrones = 999,
            CheckpointInitialMotherShips = 1
        };
        GameStatePersistence.SaveGameState();
        var loaded = GameStatePersistence.LoadGameState("CharlieB");
        Assert.IsNotNull(loaded);

        SetPrivateField(handler, "_pendingSavedState", loaded);
        SetPrivateField(handler, "_targetSceneIndex", 1);
        SetPrivateField(handler, "_pendingSceneAdvance", true);
        SetPrivateField(handler, "_pendingSceneAdvanceFramesLeft", 0);

        handler.UpdateFrame(world);

        var savedAfterLoadAttempt = GameStatePersistence.LoadGameState("CharlieB");
        Assert.IsNotNull(savedAfterLoadAttempt);
        Assert.IsTrue(savedAfterLoadAttempt!.HasCheckpoint,
            "A checkpoint that cannot be applied should not be destructively rewritten as HasCheckpoint=false during load.");
    }

    private static void SetPrivateField<T>(SceneHandler handler, string fieldName, T value)
    {
        var field = typeof(SceneHandler).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Missing private field: {fieldName}");
        field!.SetValue(handler, value);
    }

    private static GameWorld CreateWorld(SceneHandler handler)
    {
        var world = new GameWorld();
        world.SceneHandler = handler;
        world.WorldInhabitants.Clear();
        handler.SetupActiveScene(world);
        return world;
    }
}
