using _3DWorld.Scene;
using _3dRotations.World.Objects;
using _3dTesting._3dWorld;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.Persistence;
using Domain;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.SceneManagement;

/// <summary>
/// Tests covering:
///   - Pressing X returns to intro without overwriting checkpoint progress.
///   - After returning to intro the world is cleared (no bleed-through from the game scene).
///   - After returning to intro the scene resets to index 0 (Intro).
///   - SimulationRound is persisted across save/load cycles.
///   - SimulationRound is not saved by ReturnToIntro (X press).
///   - SimulationRound is NOT reset by ResetForNewGame.
/// </summary>
[TestClass]
public class ReturnToIntroAndSimulationRoundTests
{
    private string _originalLocalFolder = string.Empty;
    private string _testLocalFolder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainXTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.Initialize();

        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ObjectIdCounter = 0;
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
        catch { }
    }

    // ------------------------------------------------------------------
    // X key: existing checkpoint save must survive unchanged
    // ------------------------------------------------------------------

    [TestMethod]
    public void ReturnToIntro_SaveFileIsPreserved_NotDeleted()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 3;
        gps.Score = 5000;
        GameStatePersistence.SaveGameState();

        Assert.IsTrue(PersistenceSetup.HasPlayerSaveFile("Pilot"), "Pre-condition: save file must exist.");

        var handler = new SceneHandler();
        var world = CreateMinimalWorld(handler);

        SetCurrentSceneIndex(handler, 3);
        InvokeReturnToIntro(handler, world);

        Assert.IsTrue(PersistenceSetup.HasPlayerSaveFile("Pilot"),
            "X should NOT delete the save file — the player's progress must be preserved.");
    }

    [TestMethod]
    public void ReturnToIntro_DoesNotOverwriteExistingCheckpointSave()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 3;
        gps.Score = 5000;
        gps.TotalKills = 42;
        GameStatePersistence.SaveGameState();

        // Accumulate more score in-session before pressing X
        gps.Score = 7500;
        gps.TotalKills = 60;

        var handler = new SceneHandler();
        var world = CreateMinimalWorld(handler);
        SetCurrentSceneIndex(handler, 3);
        InvokeReturnToIntro(handler, world);

        var saved = GameStatePersistence.LoadGameState("Pilot");
        Assert.IsNotNull(saved);
        Assert.AreEqual(5000, saved.Score, "ReturnToIntro must not overwrite the last checkpoint score.");
        Assert.AreEqual(42, saved.TotalKills, "ReturnToIntro must not overwrite checkpoint kill count.");
    }

    [TestMethod]
    public void ReturnToIntro_DoesNotCreateSaveForUnsavedRun()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 3;
        gps.Score = 5000;

        Assert.IsFalse(PersistenceSetup.HasPlayerSaveFile("Pilot"), "Pre-condition: no save file should exist.");

        var handler = new SceneHandler();
        var world = CreateMinimalWorld(handler);
        SetCurrentSceneIndex(handler, 3);
        InvokeReturnToIntro(handler, world);

        Assert.IsFalse(PersistenceSetup.HasPlayerSaveFile("Pilot"),
            "ReturnToIntro must not create a save file; only checkpoints should persist progress.");
    }

    [TestMethod]
    public void ReturnToIntro_DoesNotSubmitHighscore()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 3;
        gps.Score = 99999;
        gps.TotalKills = 99;

        var handler = new SceneHandler();
        var world = CreateMinimalWorld(handler);
        SetCurrentSceneIndex(handler, 3);
        InvokeReturnToIntro(handler, world);

        var highscores = HighscoreService.LoadLocalHighscores();
        Assert.AreEqual(0, highscores.Entries.Count,
            "ReturnToIntro must not submit highscores; checkpoint flows submit them.");
    }

    // ------------------------------------------------------------------
    // X key: world must be cleared
    // ------------------------------------------------------------------

    [TestMethod]
    public void ReturnToIntro_WorldInhabitantsAreCleared()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 2;

        var handler = new SceneHandler();
        var world = CreateMinimalWorld(handler);

        // Add some fake inhabitants simulating a live game scene
        world.WorldInhabitants.Add(new _3dObject { ObjectId = 1, ObjectName = "Seeder" });
        world.WorldInhabitants.Add(new _3dObject { ObjectId = 2, ObjectName = "Ship" });

        SetCurrentSceneIndex(handler, 2);
        InvokeReturnToIntro(handler, world);

        Assert.AreEqual(0, world.WorldInhabitants.Count,
            "All world inhabitants must be cleared when returning to intro via X.");
    }

    [TestMethod]
    public void ReturnToIntro_DisposesWorldMovementsBeforeClearing()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 2;

        var handler = new SceneHandler();
        var world = CreateMinimalWorld(handler);
        var movement = new CapturingMovement();
        world.WorldInhabitants.Add(new _3dObject
        {
            ObjectId = 1,
            ObjectName = "Ship",
            Movement = movement
        });

        SetCurrentSceneIndex(handler, 2);
        InvokeReturnToIntro(handler, world);

        Assert.IsTrue(movement.WasDisposed,
            "Returning to intro must dispose movements before clearing objects so looped object audio can stop.");
    }

    // ------------------------------------------------------------------
    // X key: scene index resets to 0
    // ------------------------------------------------------------------

    [TestMethod]
    public void ReturnToIntro_SceneIndexResetsToZero()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 5;

        var handler = new SceneHandler();
        var world = CreateMinimalWorld(handler);
        SetCurrentSceneIndex(handler, 5);
        InvokeReturnToIntro(handler, world);

        Assert.AreEqual(SceneTypes.Intro, handler.GetActiveScene().SceneType,
            "Active scene must be Intro after pressing X.");
        Assert.AreEqual(0, GameState.GamePlayState.SceneIndex,
            "SceneIndex in GamePlayState must be 0 after returning to intro.");
    }

    [TestMethod]
    public void EscapeFromGame_ReturnsToIntro()
    {
        RunOnStaThread(() =>
        {
            var gps = GameState.GamePlayState;
            gps.PlayerName = "Pilot";
            gps.SceneIndex = 1;

            var handler = new SceneHandler();
            var world = CreateMinimalWorld(handler);
            SetCurrentSceneIndex(handler, 1);
            GameState.ScreenOverlayState.ShowOverlay = false;

            HandleKeyPress(handler, world, GameInputKey.Escape);

            Assert.AreEqual(SceneTypes.Intro, handler.GetActiveScene().SceneType,
                "Escape from a game scene should return to the intro/menu, not exit immediately.");
            Assert.AreEqual(0, GameState.GamePlayState.SceneIndex);
        });
    }

    [TestMethod]
    public void XFromGame_ReturnsToIntro()
    {
        RunOnStaThread(() =>
        {
            var gps = GameState.GamePlayState;
            gps.PlayerName = "Pilot";
            gps.SceneIndex = 1;

            var handler = new SceneHandler();
            var world = CreateMinimalWorld(handler);
            SetCurrentSceneIndex(handler, 1);
            GameState.ScreenOverlayState.ShowOverlay = false;

            HandleKeyPress(handler, world, GameInputKey.X);

            Assert.AreEqual(SceneTypes.Intro, handler.GetActiveScene().SceneType,
                "X from a game scene should return to the intro/menu.");
            Assert.AreEqual(0, GameState.GamePlayState.SceneIndex);
        });
    }

    // ------------------------------------------------------------------
    // SimulationRound: persisted and restored
    // ------------------------------------------------------------------

    [TestMethod]
    public void SimulationRound_IsSavedAndRestoredCorrectly()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 10;
        gps.SimulationRound = 4;
        gps.Score = 99000;
        GameStatePersistence.SaveGameState();

        var saved = GameStatePersistence.LoadGameState("Pilot");
        Assert.IsNotNull(saved);
        Assert.AreEqual(4, saved.SimulationRound,
            "SimulationRound must be persisted to the save file.");

        GameState.GamePlayState = new GamePlayState();
        GameStatePersistence.RestoreToGamePlayState(saved);

        Assert.AreEqual(4, GameState.GamePlayState.SimulationRound,
            "SimulationRound must be restored from the save file.");
    }

    [TestMethod]
    public void SimulationRound_IsNotSavedByReturnToIntro()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 10;
        gps.SimulationRound = 3;
        gps.Score = 50000;

        var handler = new SceneHandler();
        var world = CreateMinimalWorld(handler);
        SetCurrentSceneIndex(handler, 10);
        InvokeReturnToIntro(handler, world);

        Assert.IsFalse(PersistenceSetup.HasPlayerSaveFile("Pilot"),
            "ReturnToIntro must not create a save file for simulation state; checkpoints own persistence.");
    }

    [TestMethod]
    public void SimulationRound_IsNotResetByResetForNewGame()
    {
        var gps = GameState.GamePlayState;
        gps.SimulationRound = 7;

        gps.ResetForNewGame();

        Assert.AreEqual(7, gps.SimulationRound,
            "ResetForNewGame must NOT reset SimulationRound — it must escalate across rounds.");
    }

    [TestMethod]
    public void SimulationRound_StartsAtZeroForNewPlayer()
    {
        var fresh = new GamePlayState();
        Assert.AreEqual(0, fresh.SimulationRound,
            "A brand-new GamePlayState must start with SimulationRound = 0.");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static _3dWorld CreateMinimalWorld(SceneHandler handler)
    {
        var world = new _3dWorld();
        world.SceneHandler = handler;
        world.WorldInhabitants.Clear();
        return world;
    }

    private static void SetCurrentSceneIndex(SceneHandler handler, int index)
    {
        var field = typeof(SceneHandler).GetField("currentSceneIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(handler, index);
        GameState.GamePlayState.SceneIndex = index;
    }

    private static void InvokeReturnToIntro(SceneHandler handler, _3dWorld world)
    {
        var method = typeof(SceneHandler).GetMethod(
            "ReturnToIntro",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(I3dWorld) },
            modifiers: null);
        method?.Invoke(handler, new object[] { world });
    }

    private static void HandleKeyPress(SceneHandler handler, _3dWorld world, GameInputKey key)
    {
        handler.HandleKeyPress(key, world);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            throw failure;
    }

    private sealed class CapturingMovement : IObjectMovement
    {
        public bool WasDisposed { get; private set; }
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = null!;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) => theObject;
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void Dispose() => WasDisposed = true;
    }
}
