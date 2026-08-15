using TheOmegaStrain.Game.SceneManagement;
using TheOmegaStrain.Game.Scenes.Tutorial;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Common.Persistence;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Audio.Services;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class TutorialSceneTests
{
    private string _originalLocalFolder = string.Empty;
    private string _testLocalFolder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainTutorialTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.Initialize();

        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ShipState = new ShipState();
        GameState.WeatherVisualState = new WeatherVisualState();
        GameState.WorldFade = new WorldFadeState();
        GameState.TutorialState = new TutorialRuntimeState();
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
        catch
        {
        }
    }

    [TestMethod]
    public void TutorialScene_SetsTutorialSceneTypeAndOverlay()
    {
        var scene = new TutorialScene();

        scene.SetupSceneOverlay();

        Assert.AreEqual(SceneTypes.Tutorial, scene.SceneType);
        Assert.AreEqual("music_kanpai", scene.SceneMusic);
        Assert.AreEqual(ScreenOverlayType.Game, GameState.ScreenOverlayState.Type);
        Assert.IsTrue(GameState.ScreenOverlayState.ShowOverlay);
        StringAssert.Contains(GameState.ScreenOverlayState.Title, "HAL-E");
        StringAssert.Contains(GameState.ScreenOverlayState.Body, "ESC");
        StringAssert.Contains(GameState.ScreenOverlayState.Footer, "ESC");
        StringAssert.Contains(GameState.ScreenOverlayState.Footer, "SKIP");
    }

    [TestMethod]
    public void TutorialScene_AddsVoicePromptController()
    {
        var scene = new TutorialScene();
        var world = new TestWorld();

        scene.SetupScene(world);

        var prompt = world.WorldInhabitants.SingleOrDefault(o => o.ObjectName == "TutorialVoicePrompt");
        Assert.IsNotNull(prompt);
        Assert.IsInstanceOfType(prompt.Movement, typeof(TutorialVoicePromptControls));
        Assert.AreEqual(1, prompt.ObjectParts.Count);
        Assert.IsTrue(prompt.ObjectParts[0].IsVisible);
        Assert.IsTrue(prompt.ObjectParts[0].Triangles.Count > 0);
    }

    [TestMethod]
    public void TutorialScene_LoadsRecordedSurfaceAndAddsViewport()
    {
        var scene = new TutorialScene();
        var world = new TestWorld();

        scene.SetupScene(world);

        Assert.AreEqual(GameModes.Playback, scene.GameMode);
        Assert.AreEqual(Path.Combine("SceneFiles", "Scene1SurfaceRecording.retro"), GameState.SurfaceState.SurfaceFilePath);
        Assert.IsNotNull(GameState.SurfaceState.Global2DMap);
        Assert.AreNotEqual(0UL, GameState.SurfaceState.SurfaceHash);

        var ship = world.WorldInhabitants.SingleOrDefault(o => o.ObjectName == "Ship");
        Assert.IsNotNull(ship);
        Assert.IsNotNull(ship.WeaponSystems);
        Assert.IsNotNull(ship.ImpactStatus);
        Assert.AreEqual(ShipSetup.DefaultShipHealth, ship.ImpactStatus.ObjectHealth);

        var guidanceArrow = world.WorldInhabitants.SingleOrDefault(o => o.ObjectName == "SeederGuidanceArrow");
        Assert.IsNotNull(guidanceArrow);
        Assert.IsInstanceOfType(guidanceArrow.Movement, typeof(SeederGuidanceArrowControl));

        var seeders = world.WorldInhabitants.Where(o => o.ObjectName == "Seeder").ToList();
        Assert.AreEqual(3, seeders.Count);
        Assert.AreEqual(3, GameState.SurfaceState.AiObjects.Count(o => o.ObjectName == "Seeder"));
        Assert.IsTrue(seeders.All(seeder => seeder.Movement is TutorialSeederControls));
        Assert.IsTrue(seeders.All(seeder => seeder.IsActive));
        Assert.AreEqual(1, seeders.Count(seeder => seeder.HasPowerUp));
        Assert.IsTrue(seeders.All(seeder => seeder.ImpactStatus?.ObjectHealth == EnemySetup.SeederHealth));

        var drone = world.WorldInhabitants.SingleOrDefault(o => o.ObjectName == "KamikazeDrone");
        Assert.IsNotNull(drone);
        Assert.IsFalse(drone.IsActive);
        Assert.IsInstanceOfType(drone.Movement, typeof(KamikazeDroneControls));
        Assert.AreEqual(EnemySetup.KamikazeDroneHealth, drone.ImpactStatus?.ObjectHealth);
        Assert.IsTrue(GameState.SurfaceState.AiObjects.Any(o => o.ObjectId == drone.ObjectId));

        var surface = world.WorldInhabitants.SingleOrDefault(o => o.ObjectName == "Surface");
        Assert.IsNotNull(surface);
        Assert.IsNotNull(surface.ParentSurface);
        Assert.IsInstanceOfType(surface.Movement, typeof(GroundControls));
        Assert.IsFalse(surface.CrashBoxesFollowRotation);
        Assert.AreSame(surface, GameState.SurfaceState.SurfaceViewportObject);
    }

    [TestMethod]
    public void TutorialScene_DirectorActivatesDroneAfterSeedersAndPowerupThenCompletes()
    {
        var scene = new TutorialScene();
        var world = new TestWorld();
        GameState.GamePlayState.PlayerName = "Pilot";
        IGameEvent? completedEvent = null;
        world.EventBus!.Subscribe(GameEventType.SceneCompleted, gameEvent => completedEvent = gameEvent);

        scene.SetupScene(world);
        scene.Director!.Initialize(world.EventBus!, world);

        foreach (var seeder in GameState.SurfaceState.AiObjects.Where(o => o.ObjectName == "Seeder"))
            seeder.ImpactStatus!.ObjectHealth = 0;

        GameState.GamePlayState.PowerUpsCollected = 1;
        scene.Director.Update();

        var drone = GameState.SurfaceState.AiObjects.Single(o => o.ObjectName == "KamikazeDrone");
        Assert.IsFalse(drone.IsActive);

        GameState.TutorialState.DecoySelectCueSpoken = true;
        scene.Director.Update();

        Assert.IsTrue(drone.IsActive);
        Assert.AreEqual(1, GameState.GamePlayState.DronesRemaining);
        Assert.IsFalse(scene.Director.IsVictory);

        drone.ImpactStatus!.ObjectHealth = 0;
        GameState.TutorialState.CompleteCueSpoken = true;
        scene.Director.Update();

        Assert.IsTrue(scene.Director.IsVictory);
        Assert.IsTrue(TutorialProgressService.HasCompletedTutorial("Pilot"));
        Assert.IsNotNull(completedEvent);
        Assert.AreEqual(SceneTypes.Tutorial, completedEvent.SceneType);
    }

    [TestMethod]
    public void SceneHandler_KeepsExistingSceneIndexesAndAppendsTutorial()
    {
        GameState.GamePlayState.SceneIndex = 3;
        var handler = new SceneHandler();

        Assert.AreEqual("Scene3", handler.GetActiveScene().GetType().Name);

        GameState.GamePlayState = new GamePlayState { SceneIndex = 11 };
        handler = new SceneHandler();

        Assert.AreEqual(SceneTypes.Tutorial, handler.GetActiveScene().SceneType);
    }

    [TestMethod]
    public void SceneHandler_AutoStartsTutorialForPlayerWithoutLocalCompletion()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);

            GameState.ScreenOverlayState.SetNameEntryPreset("Pilot");
            HandleKeyPress(handler, world, GameInputKey.Return);
            AdvancePendingScene(handler, world);

            Assert.AreEqual(SceneTypes.Tutorial, handler.GetActiveScene().SceneType);
            Assert.IsFalse(TutorialProgressService.HasCompletedTutorial("Pilot"));
        });
    }

    [TestMethod]
    public void SceneHandler_NextSceneFromIntroRoutesUntrainedPlayerToTutorialBeforeScene1()
    {
        var handler = new SceneHandler();
        var world = CreateRealWorld(handler);
        GameState.GamePlayState.PlayerName = "Pilot";
        GameState.GamePlayState.SceneIndex = 0;

        handler.NextScene(world);

        Assert.AreEqual(SceneTypes.Tutorial, handler.GetActiveScene().SceneType);
        Assert.IsFalse(TutorialProgressService.HasCompletedTutorial("Pilot"));
    }

    [TestMethod]
    public void SceneHandler_CompletedTutorialAdvancesToFirstGameScene()
    {
        var handler = new SceneHandler();
        var world = CreateRealWorld(handler);
        GameState.GamePlayState.PlayerName = "Pilot";
        GameState.GamePlayState.SceneIndex = 11;
        GameState.GamePlayState.Score = 500;
        GameState.GamePlayState.TotalKills = 3;
        GameState.GamePlayState.TotalShotsFired = 12;
        GameState.GamePlayState.PowerUpsCollected = 1;
        TutorialProgressService.MarkTutorialCompleted("Pilot");

        var sceneIndexField = typeof(SceneHandler).GetField("currentSceneIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        sceneIndexField?.SetValue(handler, 11);

        handler.NextScene(world);

        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene1", handler.GetActiveScene().GetType().Name);
        Assert.AreEqual(1, GameState.GamePlayState.SceneIndex);
        Assert.AreEqual(SceneTypes.Game, GameState.GamePlayState.CurrentSceneType);
        Assert.AreEqual(0, GameState.GamePlayState.Score);
        Assert.AreEqual(0, GameState.GamePlayState.TotalKills);
        Assert.AreEqual(0, GameState.GamePlayState.TotalShotsFired);
        Assert.AreEqual(0, GameState.GamePlayState.PowerUpsCollected,
            "Training is teaching only: no powerups, score, kills, shots, or deaths carry into the campaign.");
    }

    [TestMethod]
    public void SceneHandler_LeavingTutorialRestoresPreTutorialCampaignProgress()
    {
        // Realistic flow: player has campaign progress, replays the tutorial, picks up the
        // training powerup, then leaves. Pre-tutorial progress must be intact and any
        // training-only score/kills/shots/deaths/powerups must be gone.
        var handler = new SceneHandler();
        var world = CreateRealWorld(handler);
        GameState.GamePlayState.PlayerName = "Pilot";

        // Pre-tutorial campaign progress (player had collected powerups, score, kills, etc.).
        GameState.GamePlayState.Score = 1250;
        GameState.GamePlayState.TotalKills = 7;
        GameState.GamePlayState.TotalShotsFired = 33;
        GameState.GamePlayState.TotalDeaths = 1;
        GameState.GamePlayState.PowerUpsCollected = 2; // Decoy + Lazer already earned.

        // Position SceneHandler at the tutorial slot.
        var sceneIndexField = typeof(SceneHandler).GetField("currentSceneIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        sceneIndexField?.SetValue(handler, 11);

        // Realistic entry into the tutorial — this captures the pre-tutorial snapshot.
        handler.SetupActiveScene(world);
        Assert.AreEqual(SceneTypes.Tutorial, handler.GetActiveScene().SceneType,
            "Test precondition: SetupActiveScene must put us into the tutorial slot.");

        // Simulate training: collect the training powerup and rack up some training stats.
        GameState.GamePlayState.PowerUpsCollected = 3;
        GameState.GamePlayState.Score = 9999;
        GameState.GamePlayState.TotalKills = 99;
        GameState.GamePlayState.TotalShotsFired = 999;
        GameState.GamePlayState.TotalDeaths = 9;
        TutorialProgressService.MarkTutorialCompleted("Pilot");

        handler.NextScene(world);

        Assert.AreEqual(SceneTypes.Game, handler.GetActiveScene().SceneType);
        Assert.AreEqual("Scene1", handler.GetActiveScene().GetType().Name);
        Assert.AreEqual(1250, GameState.GamePlayState.Score,
            "Pre-tutorial Score must be restored when leaving training.");
        Assert.AreEqual(7, GameState.GamePlayState.TotalKills,
            "Pre-tutorial TotalKills must be restored when leaving training.");
        Assert.AreEqual(33, GameState.GamePlayState.TotalShotsFired,
            "Pre-tutorial TotalShotsFired must be restored when leaving training.");
        Assert.AreEqual(1, GameState.GamePlayState.TotalDeaths,
            "Pre-tutorial TotalDeaths must be restored when leaving training.");
        Assert.AreEqual(2, GameState.GamePlayState.PowerUpsCollected,
            "Pre-tutorial PowerUpsCollected must be restored; training powerups must not leak.");
        Assert.IsTrue(TutorialProgressService.HasCompletedTutorial("Pilot"),
            "Training completion flag must persist independently of campaign progression.");
    }

    [TestMethod]
    public void SceneHandler_PersistSceneBoundaryProgress_DoesNotQueueSaveConfirmationVoice()
    {
        // The save-confirmation voice belongs to in-scene player actions (powerup pickup,
        // mothership kill). Scene boundaries are not player-driven save events, so the
        // voice must NOT be requested when the boundary save runs between scenes.
        ShipAiVoiceService.Shared.StopCurrentSpeech(); // clear any leftover state

        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 1;
        gps.CurrentSceneType = SceneTypes.Game;
        gps.HasCheckpoint = true;
        gps.CheckpointSeedersRemaining = 3;

        var method = typeof(SceneHandler).GetMethod(
            "PersistSceneBoundaryProgress",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(method, "PersistSceneBoundaryProgress must exist on SceneHandler.");

        method!.Invoke(null, new object[] { gps });

        Assert.IsFalse(gps.HasCheckpoint,
            "Boundary save must still clear the in-scene checkpoint so it does not leak into the next scene.");

        var pendingCueField = typeof(ShipAiVoiceService).GetField(
            "_pendingGameplayCue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(pendingCueField, "ShipAiVoiceService should expose _pendingGameplayCue via reflection.");
        var pendingCue = pendingCueField!.GetValue(ShipAiVoiceService.Shared);
        Assert.IsNull(pendingCue,
            "Scene boundary save must NOT queue a gameplay save-confirmation voice cue.");
    }

    [TestMethod]
    public void SceneHandler_ManualTrainingKeyStartsTutorialEvenWhenCompleted()
    {
        RunOnStaThread(() =>
        {
            TutorialProgressService.MarkTutorialCompleted("Pilot");
            GameState.GamePlayState.SceneIndex = 0;

            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            GameState.ScreenOverlayState.SetIntroPreset("THE OMEGA STRAIN");
            GameState.ScreenOverlayState.ShowOverlay = true;

            HandleKeyPress(handler, world, GameInputKey.T);

            Assert.AreEqual(ScreenOverlayType.NameEntry, GameState.ScreenOverlayState.Type);
            Assert.IsTrue(GameState.ScreenOverlayState.ShowOverlay);

            GameState.ScreenOverlayState.NameEntryBuffer = "Pilot";
            HandleKeyPress(handler, world, GameInputKey.Return);
            AdvancePendingScene(handler, world);

            Assert.AreEqual(SceneTypes.Tutorial, handler.GetActiveScene().SceneType);
        });
    }

    [TestMethod]
    public void SceneHandler_ManualTrainingReturnsPlayerToSavedScene()
    {
        RunOnStaThread(() =>
        {
            var savedState = GameState.GamePlayState;
            savedState.PlayerName = "Pilot";
            savedState.SceneIndex = 6;
            savedState.CurrentSceneType = SceneTypes.Game;
            savedState.Score = 12500;
            savedState.TotalKills = 42;
            savedState.PowerUpsCollected = 2;
            GameStatePersistence.SaveGameState();
            TutorialProgressService.MarkTutorialCompleted("Pilot");

            GameState.GamePlayState = new GamePlayState();
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            GameState.ScreenOverlayState.SetIntroPreset("THE OMEGA STRAIN");
            GameState.ScreenOverlayState.ShowOverlay = true;

            HandleKeyPress(handler, world, GameInputKey.T);
            GameState.ScreenOverlayState.NameEntryBuffer = "Pilot";
            HandleKeyPress(handler, world, GameInputKey.Return);
            AdvancePendingScene(handler, world);

            Assert.AreEqual(SceneTypes.Tutorial, handler.GetActiveScene().SceneType);

            GameState.GamePlayState.Score = 99999;
            GameState.GamePlayState.TotalKills = 999;
            GameState.GamePlayState.PowerUpsCollected = 3;
            handler.NextScene(world);

            Assert.AreEqual("Scene6", handler.GetActiveScene().GetType().Name);
            Assert.AreEqual(6, GameState.GamePlayState.SceneIndex);
            Assert.AreEqual(12500L, GameState.GamePlayState.Score);
            Assert.AreEqual(42, GameState.GamePlayState.TotalKills);
            Assert.AreEqual(2, GameState.GamePlayState.PowerUpsCollected);
        });
    }

    [TestMethod]
    public void SceneHandler_SkippingTutorialMarksLocalCompletionForPlayer()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            GameState.GamePlayState.PlayerName = "Pilot";
            GameState.GamePlayState.SceneIndex = 11;

            var sceneIndexField = typeof(SceneHandler).GetField("currentSceneIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sceneIndexField?.SetValue(handler, 11);

            GameState.ScreenOverlayState.ShowOverlay = true;
            HandleKeyPress(handler, world, GameInputKey.X);

            Assert.IsTrue(TutorialProgressService.HasCompletedTutorial("Pilot"));
            Assert.AreEqual(SceneTypes.Intro, handler.GetActiveScene().SceneType);
        });
    }

    [TestMethod]
    public void SceneHandler_TutorialInstructionOverlayRequiresMinimumHoldBeforeResume()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            GameState.GamePlayState.PlayerName = "Pilot";
            GameState.GamePlayState.SceneIndex = 11;

            var sceneIndexField = typeof(SceneHandler).GetField("currentSceneIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sceneIndexField?.SetValue(handler, 11);
            var shipControls = new ShipControls
            {
                rotationZ = 67,
                tilt = 44
            };
            var ship = CreateRotatedTutorialShip(shipControls);
            ship.ObjectOffsets = new Vector3 { x = 12f, y = 34f, z = 456f };
            ship.WorldPosition = new Vector3 { x = 1000f, y = 22f, z = 2000f };
            shipControls.CaptureOverlayPauseTransform(ship);
            ship.ObjectOffsets = new Vector3 { x = -100f, y = -200f, z = -300f };
            ship.WorldPosition = new Vector3 { x = -400f, y = -500f, z = -600f };
            ship.Rotation = new Vector3 { x = 1f, y = 2f, z = 3f };
            shipControls.rotationZ = -9;
            shipControls.tilt = -8;
            world.WorldInhabitants.Add(ship);

            GameState.ScreenOverlayState.ShowOverlay = true;
            GameState.ScreenOverlayState.Type = ScreenOverlayType.Tutorial;
            GameState.TutorialState.ShowInstructionOverlay("TutorialIntro");
            world.IsPaused = true;

            HandleKeyPress(handler, world, GameInputKey.X);

            Assert.AreEqual(SceneTypes.Tutorial, handler.GetActiveScene().SceneType);
            Assert.IsTrue(GameState.ScreenOverlayState.ShowOverlay);
            Assert.AreEqual(ScreenOverlayType.Tutorial, GameState.ScreenOverlayState.Type);
            Assert.IsTrue(GameState.TutorialState.InstructionOverlayPauseActive);
            Assert.IsTrue(world.IsPaused);

            HandleKeyPress(handler, world, GameInputKey.Escape);

            Assert.AreEqual(SceneTypes.Tutorial, handler.GetActiveScene().SceneType);
            Assert.IsFalse(GameState.ScreenOverlayState.ShowOverlay);
            Assert.AreEqual(ScreenOverlayType.Game, GameState.ScreenOverlayState.Type);
            Assert.IsFalse(GameState.TutorialState.InstructionOverlayPauseActive);
            Assert.IsFalse(world.IsPaused);
            Assert.AreEqual(67, shipControls.rotationZ);
            Assert.AreEqual(44, shipControls.tilt);
            Assert.AreEqual(400, shipControls.zoom);
            Assert.AreEqual(12f, ship.ObjectOffsets!.x, 0.001f);
            Assert.AreEqual(34f, ship.ObjectOffsets.y, 0.001f);
            Assert.AreEqual(400f, ship.ObjectOffsets.z, 0.001f);
            Assert.AreEqual(1000f, ship.WorldPosition!.x, 0.001f);
            Assert.AreEqual(22f, ship.WorldPosition.y, 0.001f);
            Assert.AreEqual(2000f, ship.WorldPosition.z, 0.001f);
            Assert.IsTrue(GameState.ShipState.ShipCrashDetectionDisabledUntilUtc > DateTime.UtcNow);
            Assert.IsTrue(GameState.ShipState.ShipGravityDisabledUntilUtc > DateTime.UtcNow);
        });
    }

    [TestMethod]
    public void SceneHandler_EscapeOnTutorialOverlayClosesOverlayBeforeMenuExit()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            GameState.GamePlayState.PlayerName = "Pilot";
            GameState.GamePlayState.SceneIndex = 11;

            var sceneIndexField = typeof(SceneHandler).GetField("currentSceneIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sceneIndexField?.SetValue(handler, 11);
            var shipControls = new ShipControls
            {
                rotationZ = 35,
                tilt = -20
            };
            var ship = CreateRotatedTutorialShip(shipControls);
            ship.ObjectOffsets = new Vector3 { x = 7f, y = 8f, z = 400f };
            ship.WorldPosition = new Vector3 { x = 900f, y = 10f, z = 1200f };
            shipControls.CaptureOverlayPauseTransform(ship);
            ship.ObjectOffsets = new Vector3 { x = -7f, y = -8f, z = -400f };
            ship.WorldPosition = new Vector3 { x = -900f, y = -10f, z = -1200f };
            ship.Rotation = new Vector3 { x = 3f, y = 4f, z = 5f };
            shipControls.rotationZ = -35;
            shipControls.tilt = 99;
            world.WorldInhabitants.Add(ship);

            GameState.ScreenOverlayState.ShowOverlay = true;
            GameState.ScreenOverlayState.Type = ScreenOverlayType.Game;
            world.IsPaused = true;

            HandleKeyPress(handler, world, GameInputKey.Escape);

            Assert.AreEqual(SceneTypes.Tutorial, handler.GetActiveScene().SceneType);
            Assert.IsFalse(GameState.ScreenOverlayState.ShowOverlay);
            Assert.IsFalse(world.IsPaused);
            Assert.AreEqual(35, shipControls.rotationZ);
            Assert.AreEqual(-20, shipControls.tilt);
            Assert.AreEqual(400, shipControls.zoom);
            Assert.AreEqual(7f, ship.ObjectOffsets!.x, 0.001f);
            Assert.AreEqual(8f, ship.ObjectOffsets.y, 0.001f);
            Assert.AreEqual(400f, ship.ObjectOffsets.z, 0.001f);
            Assert.AreEqual(900f, ship.WorldPosition!.x, 0.001f);
            Assert.AreEqual(10f, ship.WorldPosition.y, 0.001f);
            Assert.AreEqual(1200f, ship.WorldPosition.z, 0.001f);
            Assert.IsTrue(GameState.ShipState.ShipCrashDetectionDisabledUntilUtc > DateTime.UtcNow);
            Assert.IsTrue(GameState.ShipState.ShipGravityDisabledUntilUtc > DateTime.UtcNow);

            HandleKeyPress(handler, world, GameInputKey.Escape);

            Assert.AreEqual(SceneTypes.Intro, handler.GetActiveScene().SceneType);
        });
    }

    [TestMethod]
    public void TutorialRuntimeState_AutoClosesAfterVoiceWindow()
    {
        var state = new TutorialRuntimeState();
        DateTime shownAt = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        state.ShowInstructionOverlay("TutorialWeapons", shownAt, autoCloseSeconds: 8.0);

        Assert.IsFalse(state.CanCloseInstructionOverlay(shownAt.AddSeconds(4.9)));
        Assert.IsTrue(state.CanCloseInstructionOverlay(shownAt.AddSeconds(5.1)));
        Assert.IsFalse(state.ShouldAutoCloseInstructionOverlay(shownAt.AddSeconds(7.9)));
        Assert.IsTrue(state.ShouldAutoCloseInstructionOverlay(shownAt.AddSeconds(8.1)));
    }

    [TestMethod]
    public void IntroOverlay_TextMentionsManualTrainingKey()
    {
        var intro = new TheOmegaStrain.Game.Scenes.Intro.Intro();

        intro.SetupSceneOverlay();

        Assert.IsTrue(GameState.ScreenOverlayState.Pages.Any(page => page.Any(text => text.Contains("[T] TRAINING"))));
        var controls = GameState.ScreenOverlayState.Pages.Single(page => page[1] == "FLIGHT CONTROLS")[2];
        StringAssert.Contains(controls, "[T] TUTORIAL");
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; } = new GameEventBus();
        public bool IsPaused { get; set; }
    }

    private static GameWorld CreateRealWorld(SceneHandler handler)
    {
        var world = new GameWorld
        {
            SceneHandler = handler
        };
        world.WorldInhabitants.Clear();
        return world;
    }

    private static _3dObject CreateRotatedTutorialShip(ShipControls controls)
    {
        return new _3dObject
        {
            ObjectId = 1001,
            ObjectName = "Ship",
            Movement = controls,
            Rotation = new Vector3 { x = 20f, y = 0f, z = 35f },
            ObjectOffsets = new Vector3(),
            WorldPosition = new Vector3(),
            ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth },
            ObjectParts = new List<I3dObjectPart>(),
            CrashBoxes = new List<List<IVector3>>()
        };
    }

    private static void AdvancePendingScene(SceneHandler handler, GameWorld world)
    {
        for (int i = 0; i < 8; i++)
            handler.UpdateFrame(world);
    }

    private static void HandleKeyPress(SceneHandler handler, GameWorld world, GameInputKey key)
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
}
