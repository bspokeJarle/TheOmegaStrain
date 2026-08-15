using TheOmegaStrain.Game.Scenes.Scene1;
using TheOmegaStrain.Game.Scenes.Scene2;
using TheOmegaStrain.Game.Scenes.Scene3;
using TheOmegaStrain.Game.Scenes.Scene4;
using TheOmegaStrain.Game.Scenes.Scene5;
using TheOmegaStrain.Game.Scenes.Scene6;
using TheOmegaStrain.Game.Scenes.Scene7;
using TheOmegaStrain.Game.Scenes.Scene8;
using TheOmegaStrain.Game.Scenes.Intro;
using TheOmegaStrain.Game.Scenes.Outro;
using TheOmegaStrain.Game.Scenes.SceneSimulation;
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Game.SceneManagement
{
    public class SceneHandler : ISceneHandler
    {
        private List<IScene> scenes = new List<IScene> { new Intro(), new Scene1(), new Scene2(), new Scene3(), new Scene4(), new Scene5(), new Scene6(), new Scene7(), new Scene8(), new Outro(), new SceneSimulation(), new TutorialScene() };
        private int currentSceneIndex = 0;
        private const bool enableLogging = false;
        private const int SceneAdvanceDelayFrames = 5;
        private bool _pendingSceneAdvance = false;
        private int _pendingSceneAdvanceFramesLeft = 0;
        private int? _targetSceneIndex = null;
        private bool _pendingNextScene = false;
        private bool _pendingTutorialStart = false;
        private SavedGameState? _pendingSavedState = null;
        private SavedGameState? _tutorialResumeSavedState = null;
        private int _settingsReturnIntroPage = 0;

        // Snapshot of campaign progression captured the moment we enter the tutorial scene,
        // so leaving the tutorial restores exactly what the player had before training (and
        // training-only pickups / score / kills disappear). Null means "no active snapshot".
        private TutorialEntrySnapshot? _tutorialEntrySnapshot = null;

        private readonly struct TutorialEntrySnapshot
        {
            public TutorialEntrySnapshot(long score, int kills, int shots, int deaths, int powerUps, int speedPowerUpLevel)
            {
                Score = score;
                TotalKills = kills;
                TotalShotsFired = shots;
                TotalDeaths = deaths;
                PowerUpsCollected = powerUps;
                SpeedPowerUpLevel = speedPowerUpLevel;
            }

            public long Score { get; }
            public int TotalKills { get; }
            public int TotalShotsFired { get; }
            public int TotalDeaths { get; }
            public int PowerUpsCollected { get; }
            public int SpeedPowerUpLevel { get; }
        }

        public SceneHandler()
        {
            ApplySceneIndexOverrideFromGameState();
        }

        public IScene GetActiveScene() => scenes[currentSceneIndex];

        // -----------------------------------------------------------------
        // Scene lifecycle
        // -----------------------------------------------------------------

        public void SetupActiveScene(I3dWorld world)
        {
            PersistenceSetup.Initialize();
            GameSettingsPersistence.LoadIntoGameState();
            ApplySceneIndexOverrideFromGameState();
            ResetSurfaceState();
            var scene = GetActiveScene();
            CaptureTutorialEntrySnapshotIfNeeded(scene);
            scene.SetupSceneOverlay();
            ClearVideoOverlay();
            ApplySceneSettings(scene);
            scene.SetupScene((GameWorld)world);
            InitializeDirector(scene, world);
            ValidateGameSceneSetup(scene, world);
            CapturePlanetStartSnapshotIfNeeded(scene);
        }

        private void CaptureTutorialEntrySnapshotIfNeeded(IScene scene)
        {
            // Snapshot pre-tutorial campaign progression so it can be restored when the
            // player leaves training. Only capture on entry; if we are already inside the
            // tutorial (e.g. ResetActiveScene during training) keep the original snapshot.
            if (scene.SceneType != SceneTypes.Tutorial || _tutorialEntrySnapshot != null)
                return;

            var gps = GameState.GamePlayState;
            _tutorialEntrySnapshot = new TutorialEntrySnapshot(
                gps.Score,
                gps.TotalKills,
                gps.TotalShotsFired,
                gps.TotalDeaths,
                gps.PowerUpsCollected,
                gps.SpeedPowerUpLevel);
        }

        public void ResetActiveScene(I3dWorld world)
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log("Scenehandler: ResetActiveScene");

            DisposeDirector();
            var newScene = CreateFreshScene();
            if (newScene == null)
                throw new InvalidOperationException($"Failed to create a new instance of scene type {GetActiveScene().GetType()}.");

            var gps = GameState.GamePlayState;

            bool hadCheckpoint = gps.HasCheckpoint;
            var snapshot = hadCheckpoint ? gps.CaptureCheckpointSnapshot() : default;

            // Capture cumulative stats before reset so they survive when there is no checkpoint
            long prevScore = gps.Score;
            int prevLives = gps.Lives;
            int prevKills = gps.TotalKills;
            int prevShots = gps.TotalShotsFired;
            int prevDeaths = gps.TotalDeaths;
            int prevPowerUps = gps.PowerUpsCollected;
            int prevSpeedPowerUpLevel = gps.SpeedPowerUpLevel;

            ClearVideoOverlay();
            gps.ResetForNewGame();
            ResetSurfaceState();

            scenes[currentSceneIndex] = newScene;
            GameState.ScreenOverlayState.HardHide();
            newScene.SetupGameOverlay();
            ApplySceneSettings(newScene);
            newScene.SetupScene((GameWorld)world);
            ApplySceneSettings(newScene);

            if (hadCheckpoint)
            {
                int restoredMotherShips = ResolveRestoredMotherShipCount(snapshot.MotherShipsRemaining);

                TrimEnemies(world, "Seeder", snapshot.SeedersRemaining);
                TrimEnemies(world, "KamikazeDrone", snapshot.DronesRemaining);
                TrimEnemies(world, "MotherShipSmall", restoredMotherShips);
                TrimEnemies(world, "MotherShipMedium", restoredMotherShips);
                TrimEnemies(world, "MotherShipLarge", restoredMotherShips);
                gps.ApplyCheckpointRestart(snapshot);

                var aiObjs = GameState.SurfaceState.AiObjects;

                // If checkpoint was taken during mothership phase (all seeders/drones cleared),
                // restore mothership combat immediately after reset.
                if (snapshot.SeedersRemaining == 0 && snapshot.DronesRemaining == 0)
                {
                    for (int i = 0; i < aiObjs.Count; i++)
                    {
                        if ((aiObjs[i].ObjectName == "MotherShipSmall" || aiObjs[i].ObjectName == "MotherShipMedium" || aiObjs[i].ObjectName == "MotherShipLarge") && !aiObjs[i].IsActive)
                            aiObjs[i].IsActive = true;
                    }
                }

                // If decoy was unlocked at checkpoint time, restore active drone phase too.
                if (gps.IsDecoyUnlocked)
                {
                    for (int i = 0; i < aiObjs.Count; i++)
                    {
                        if (aiObjs[i].ObjectName == "KamikazeDrone" && !aiObjs[i].IsActive)
                            aiObjs[i].IsActive = true;
                    }
                }

                if (Logger.ShouldLog(enableLogging)) Logger.Log($"Scenehandler: Checkpoint restored. Score={gps.Score} Lives={gps.Lives} Kills={gps.TotalKills}");
            }
            else
            {
                // No checkpoint — preserve accumulated stats with death penalty
                gps.Score = prevScore;
                gps.Lives = Math.Max(0, prevLives - 1);
                gps.TotalKills = prevKills;
                gps.TotalShotsFired = prevShots;
                gps.TotalDeaths = prevDeaths + 1;
                gps.PowerUpsCollected = prevPowerUps;
                gps.SpeedPowerUpLevel = prevSpeedPowerUpLevel;

                if (Logger.ShouldLog(enableLogging)) Logger.Log($"Scenehandler: No checkpoint — stats preserved. Score={gps.Score} Lives={gps.Lives} Kills={gps.TotalKills}");
            }

            InitializeDirector(newScene, world);
        }

        public void ResetActiveSceneToPlanetStart(I3dWorld world)
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log("Scenehandler: ResetActiveSceneToPlanetStart");

            DisposeDirector();
            var newScene = CreateFreshScene();
            if (newScene == null)
                throw new InvalidOperationException($"Failed to create a new instance of scene type {GetActiveScene().GetType()}.");

            var gps = GameState.GamePlayState;
            int sceneIndex = gps.SceneIndex;
            bool hasPlanetStartSnapshot =
                gps.HasPlanetStartSnapshot &&
                gps.PlanetStartSceneIndex == gps.SceneIndex;
            var planetStartSnapshot = hasPlanetStartSnapshot
                ? gps.CapturePlanetStartSnapshot()
                : default;

            int prevPowerUps = gps.PowerUpsCollected;
            int prevSpeedPowerUpLevel = gps.SpeedPowerUpLevel;

            ClearVideoOverlay();
            gps.ResetForNewGame();
            gps.SceneIndex = sceneIndex;
            ResetSurfaceState();

            scenes[currentSceneIndex] = newScene;
            GameState.ScreenOverlayState.HardHide();
            newScene.SetupGameOverlay();
            ApplySceneSettings(newScene);
            newScene.SetupScene((GameWorld)world);
            ApplySceneSettings(newScene);

            if (hasPlanetStartSnapshot)
            {
                gps.RestorePlanetStartSnapshotFields(planetStartSnapshot, sceneIndex);
                gps.ApplyPlanetStartSnapshot();
            }
            else
            {
                // Legacy saves have no trustworthy arrival totals. Keep durable
                // unlocks, but do not retain rewards earned on the lost planet.
                gps.PowerUpsCollected = prevPowerUps;
                gps.SpeedPowerUpLevel = prevSpeedPowerUpLevel;
                gps.InfectionLevel = 0f;
                gps.PlanetStyleBonusScore = 0;
                gps.PlanetStyleBonusSceneIndex = sceneIndex;
                gps.ClearCheckpoint();
            }

            SyncGameplayEnemyCountsFromScene(resetInitialCounts: true);
            gps.SavePlanetStartSnapshot();
            try { GameStatePersistence.SaveGameState(allowScoreRollback: true); } catch { }
            InitializeDirector(newScene, world);
        }

        public void NextScene(I3dWorld world)
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log($"Scenehandler: NextScene :{GameState.ScreenOverlayState.ShowOverlay} ");

            DisposeDirector();
            var gps = GameState.GamePlayState;
            var currentScene = GetActiveScene();
            int completedSceneIndex = gps.SceneIndex > 0 ? gps.SceneIndex : currentSceneIndex;
            var completedSceneType = currentScene.SceneType;
            bool isOutro = currentScene.SceneType == SceneTypes.Outro;
            bool isSimulation = currentScene.SceneType == SceneTypes.Simulation;
            bool isTutorial = currentScene.SceneType == SceneTypes.Tutorial;

            if (isTutorial && _tutorialResumeSavedState != null)
            {
                var saved = _tutorialResumeSavedState;
                _tutorialResumeSavedState = null;
                _tutorialEntrySnapshot = null;
                _pendingSavedState = saved;
                _targetSceneIndex = ResolveSavedSceneIndex(saved);
                _pendingNextScene = false;
                _pendingSceneAdvance = true;
                _pendingSceneAdvanceFramesLeft = 0;
                UpdateFrame(world);
                return;
            }

            // Capture cumulative stats before reset
            long prevScore = gps.Score;
            int prevKills = gps.TotalKills;
            int prevShots = gps.TotalShotsFired;
            int prevDeaths = gps.TotalDeaths;
            int prevPowerUps = gps.PowerUpsCollected;
            int prevSpeedPowerUpLevel = gps.SpeedPowerUpLevel;

            if (isOutro || isSimulation)
            {
                // After Outro or completing a Simulation round, always go to Simulation
                gps.SimulationRound++;

                // Replace the simulation slot with a fresh instance for the new round
                int simIndex = scenes.FindIndex(s => s.SceneType == SceneTypes.Simulation);
                if (simIndex < 0) simIndex = scenes.Count - 1;
                scenes[simIndex] = new SceneSimulation();
                currentSceneIndex = simIndex;

                // Clear all objects from the previous scene (Outro ship, asteroids,
                // surface, landing pad, astronaut, fireworks, particles) so nothing
                // bleeds through into the new Simulation scene.
                world.WorldInhabitants.Clear();
                if (GameState.SurfaceState.AiObjects != null)
                    GameState.SurfaceState.AiObjects.Clear();

                ClearVideoOverlay();
                gps.ResetForNewGame();
                // Keep SimulationRound — it was incremented above and ResetForNewGame does not touch it
                gps.SceneIndex = currentSceneIndex;
                ResetSurfaceState();
                SetupActiveScene(world);

                // Carry forward score and stats into simulation
                gps.Score = prevScore;
                gps.TotalKills = prevKills;
                gps.TotalShotsFired = prevShots;
                gps.TotalDeaths = prevDeaths;
                gps.PowerUpsCollected = prevPowerUps;
                gps.SpeedPowerUpLevel = prevSpeedPowerUpLevel;

                gps.SavePlanetStartSnapshot();
                PublishSceneCompleted(world, gps, completedSceneIndex, completedSceneType);
                PersistSceneBoundaryProgress(gps);

                return;
            }

            currentSceneIndex = isTutorial
                ? GetFirstGameSceneIndex()
                : (currentSceneIndex + 1) % scenes.Count;

            currentSceneIndex = ApplyTutorialGate(currentSceneIndex);

            // Game completed normally (wrapped to 0 or Outro index) — delete save
            if (currentSceneIndex == 0)
            {
                try { GameStatePersistence.DeleteSave(gps.PlayerName); } catch { }
            }

            ClearVideoOverlay();
            gps.ResetForNewGame();
            gps.SceneIndex = currentSceneIndex;
            ResetSurfaceState();
            SetupActiveScene(world);

            // Carry forward score, stats, and powerups into game and simulation scenes
            var nextScene = GetActiveScene();
            if (!isTutorial &&
                (nextScene.SceneType == SceneTypes.Game || nextScene.SceneType == SceneTypes.Simulation))
            {
                gps.Score = prevScore;
                gps.TotalKills = prevKills;
                gps.TotalShotsFired = prevShots;
                gps.TotalDeaths = prevDeaths;
                gps.PowerUpsCollected = prevPowerUps;
                gps.SpeedPowerUpLevel = prevSpeedPowerUpLevel;
            }
            else if (isTutorial &&
                (nextScene.SceneType == SceneTypes.Game || nextScene.SceneType == SceneTypes.Simulation))
            {
                // Training is teaching only. The "training completed" flag is persisted via
                // TutorialProgressService; nothing else from the tutorial should leak into
                // the campaign. Restore the campaign progression snapshot we captured the
                // moment the tutorial started — so pre-tutorial progress is preserved exactly
                // and tutorial-only pickups / kills / shots / score disappear.
                var snapshot = _tutorialEntrySnapshot;
                if (snapshot.HasValue)
                {
                    gps.Score = snapshot.Value.Score;
                    gps.TotalKills = snapshot.Value.TotalKills;
                    gps.TotalShotsFired = snapshot.Value.TotalShotsFired;
                    gps.TotalDeaths = snapshot.Value.TotalDeaths;
                    gps.PowerUpsCollected = snapshot.Value.PowerUpsCollected;
                    gps.SpeedPowerUpLevel = snapshot.Value.SpeedPowerUpLevel;
                }
                else
                {
                    // Defensive fallback: no snapshot means we entered the tutorial through a
                    // path that bypassed SetupActiveScene. Treat it as a fresh campaign start.
                    gps.Score = 0;
                    gps.TotalKills = 0;
                    gps.TotalShotsFired = 0;
                    gps.TotalDeaths = 0;
                    gps.PowerUpsCollected = 0;
                    gps.SpeedPowerUpLevel = 0;
                }

                _tutorialEntrySnapshot = null;
            }

            if (nextScene.SceneType == SceneTypes.Game || nextScene.SceneType == SceneTypes.Simulation)
            {
                gps.SavePlanetStartSnapshot();
            }

            if (currentScene.SceneType == SceneTypes.Game && IsSceneBoundarySaveTarget(nextScene))
            {
                PublishSceneCompleted(world, gps, completedSceneIndex, completedSceneType);
                PersistSceneBoundaryProgress(gps);
            }

        }

        public void UpdateFrame(I3dWorld world)
        {
            if (!_pendingSceneAdvance)
                return;

            if (_pendingSceneAdvanceFramesLeft > 0)
            {
                _pendingSceneAdvanceFramesLeft--;
                return;
            }

            _pendingSceneAdvance = false;

            if (_pendingNextScene)
            {
                _pendingNextScene = false;
                NextScene(world);
                return;
            }

            if (_targetSceneIndex.HasValue)
            {
                currentSceneIndex = _targetSceneIndex.Value;
                _targetSceneIndex = null;
            }
            else
            {
                currentSceneIndex = (currentSceneIndex + 1) % scenes.Count;
            }

            currentSceneIndex = ApplyTutorialGate(currentSceneIndex);
            var pendingSavedState = _pendingSavedState;
            if (pendingSavedState != null && scenes[currentSceneIndex].SceneType == SceneTypes.Simulation)
            {
                GameState.GamePlayState.SimulationRound = pendingSavedState.SimulationRound;
                scenes[currentSceneIndex] = new SceneSimulation();
            }

            DisposeDirector();
            ClearWorldRuntimeState(world);
            GameState.GamePlayState.SceneIndex = currentSceneIndex;
            ResetSurfaceState();
            SetupActiveScene(world);

            // Restore score and combat stats from saved game so the player builds upon them
            if (pendingSavedState != null)
            {
                var gps = GameState.GamePlayState;
                gps.SimulationRound = pendingSavedState.SimulationRound;

                GameStatePersistence.RestoreToGamePlayState(pendingSavedState);
                gps.SceneIndex = currentSceneIndex;
                ApplySceneSettings(GetActiveScene());

                // If there's a checkpoint, trim enemies and restore full checkpoint state
                bool shouldRestoreCheckpoint =
                    pendingSavedState.HasCheckpoint &&
                    SavedCheckpointMatchesActiveScene(pendingSavedState);

                if (shouldRestoreCheckpoint)
                {
                    var snapshot = gps.CaptureCheckpointSnapshot();
                    ApplyCheckpointSnapshotToCurrentState(gps, snapshot);
                    int restoredMotherShips = ResolveRestoredMotherShipCount(snapshot.MotherShipsRemaining);

                    TrimEnemies(world, "Seeder", snapshot.SeedersRemaining);
                    TrimEnemies(world, "KamikazeDrone", snapshot.DronesRemaining);
                    TrimEnemies(world, "MotherShipSmall", restoredMotherShips);
                    TrimEnemies(world, "MotherShipMedium", restoredMotherShips);
                    TrimEnemies(world, "MotherShipLarge", restoredMotherShips);

                    // Activate enemies that should already be active at this checkpoint
                    if (snapshot.SeedersRemaining == 0 && snapshot.DronesRemaining == 0)
                    {
                        ActivateMotherShips(world);
                    }

                    // Re-initialize director so it sees the trimmed enemy state
                    var scene = GetActiveScene();
                    scene.Director?.Initialize(world.EventBus!, world);
                }
                else if (pendingSavedState.HasCheckpoint)
                {
                    ClearCheckpointState(gps);
                }

                // Always apply decoy-driven drone activation when loading a saved game,
                // even when there is no checkpoint snapshot.
                if (gps.IsDecoyUnlocked)
                {
                    var aiObjs = GameState.SurfaceState.AiObjects;
                    for (int i = 0; i < aiObjs.Count; i++)
                    {
                        if (aiObjs[i].ObjectName == "KamikazeDrone" && !aiObjs[i].IsActive)
                            aiObjs[i].IsActive = true;
                    }
                }

                _pendingSavedState = null;
            }
        }

        // -----------------------------------------------------------------
        // Key handling
        // -----------------------------------------------------------------

        public void HandleKeyPress(GameInputKey key, I3dWorld world)
        {
            var scene = GetActiveScene();
            var overlay = GameState.ScreenOverlayState;

            if (overlay.Type == ScreenOverlayType.NameEntry && overlay.ShowOverlay)
            {
                HandleNameEntryKey(key, scene, overlay);
                return;
            }

            if (overlay.Type == ScreenOverlayType.Settings && overlay.ShowOverlay)
            {
                HandleSettingsOverlayKey(key, scene, overlay);
                return;
            }

            if (overlay.ShowOverlay && overlay.ChoiceAction == ScreenOverlayChoiceAction.PlanetLostRecovery)
            {
                HandlePlanetLostRecoveryChoice(key, world, overlay);
                return;
            }

            if (CanOpenSettingsOverlay(scene, overlay) && TryOpenSettingsOverlay(key, scene, overlay))
            {
                return;
            }

            if (scene.SceneType == SceneTypes.Tutorial &&
                overlay.ShowOverlay &&
                GameState.TutorialState.InstructionOverlayPauseActive)
            {
                if (key == GameInputKey.Escape)
                {
                    CloseTutorialOverlayAndResume(scene, world);
                    return;
                }

                if (!GameState.TutorialState.CanCloseInstructionOverlay(DateTime.UtcNow))
                    return;

                CloseTutorialOverlayAndResume(scene, world);
                return;
            }

            if (scene.SceneType == SceneTypes.Tutorial &&
                overlay.ShowOverlay &&
                key == GameInputKey.Escape)
            {
                CloseTutorialOverlayAndResume(scene, world);
                return;
            }

            if (scene.SceneType == SceneTypes.Tutorial && IsMenuExitKey(key))
            {
                ReturnToIntro(world, persistCurrentRun: false);
                return;
            }

            if (scene.SceneType == SceneTypes.Tutorial && overlay.ShowOverlay)
            {
                CloseTutorialOverlayAndResume(scene, world);
                return;
            }

            if (!overlay.ShowOverlay)
            {
                if (scene.SceneType == SceneTypes.Intro)
                    SkipLogoCube(world, scene);
                else if ((scene.SceneType == SceneTypes.Game || scene.SceneType == SceneTypes.Simulation) && IsMenuExitKey(key))
                    ReturnToIntro(world);
                return;
            }

            if (scene.SceneType == SceneTypes.Intro)
            {
                HandleIntroKey(overlay, key);
                return;
            }

            if (scene.SceneType == SceneTypes.Outro && overlay.Type == ScreenOverlayType.Outro)
            {
                if (IsMenuExitKey(key))
                {
                    ReturnToIntro(world);
                    return;
                }

                // Page navigation with arrow keys; any other key deploys to the Simulation scene.
                if (overlay.HasMultiplePages)
                {
                    if (key == GameInputKey.Right || key == GameInputKey.D)
                    {
                        overlay.NextPage();
                        RefreshCurrentHighscorePage(overlay);
                        return;
                    }
                    if (key == GameInputKey.Left || key == GameInputKey.A)
                    {
                        overlay.PreviousPage();
                        RefreshCurrentHighscorePage(overlay);
                        return;
                    }
                }

                overlay.HardHide();
                _pendingNextScene = true;
                _pendingSceneAdvance = true;
                _pendingSceneAdvanceFramesLeft = SceneAdvanceDelayFrames;
                return;
            }

            if (scene.SceneType == SceneTypes.Game || scene.SceneType == SceneTypes.Simulation)
            {
                if (IsMenuExitKey(key))
                {
                    ReturnToIntro(world);
                    return;
                }
                HandleGameKey(key, scene, overlay);
            }
        }

        public void HandleOverlayActivation(I3dWorld world)
        {
            var scene = GetActiveScene();
            var overlay = GameState.ScreenOverlayState;

            if (!overlay.ShowOverlay)
            {
                if (scene.SceneType == SceneTypes.Intro)
                    SkipLogoCube(world, scene);
                return;
            }

            if (overlay.Type == ScreenOverlayType.NameEntry ||
                overlay.ChoiceAction == ScreenOverlayChoiceAction.PlanetLostRecovery)
            {
                return;
            }

            if (!overlay.CanDismissWithInput)
            {
                return;
            }

            if (overlay.Type == ScreenOverlayType.Settings)
            {
                CloseSettingsOverlay(scene, overlay);
                return;
            }

            if (scene.SceneType == SceneTypes.Tutorial &&
                GameState.TutorialState.InstructionOverlayPauseActive)
            {
                if (!GameState.TutorialState.CanCloseInstructionOverlay(DateTime.UtcNow))
                    return;

                CloseTutorialOverlayAndResume(scene, world);
                return;
            }

            if (scene.SceneType == SceneTypes.Tutorial)
            {
                CloseTutorialOverlayAndResume(scene, world);
                return;
            }

            if (scene.SceneType == SceneTypes.Intro)
            {
                ShowNameEntryOverlay(overlay);
                return;
            }

            if (scene.SceneType == SceneTypes.Outro && overlay.Type == ScreenOverlayType.Outro)
            {
                overlay.HardHide();
                _pendingNextScene = true;
                _pendingSceneAdvance = true;
                _pendingSceneAdvanceFramesLeft = SceneAdvanceDelayFrames;
                return;
            }

            if (scene.SceneType == SceneTypes.Game || scene.SceneType == SceneTypes.Simulation)
            {
                scene.SetupGameOverlay();
            }
        }

        private void HandleNameEntryKey(GameInputKey key, IScene scene, ScreenOverlayState overlay)
        {
            if (key == GameInputKey.Escape)
            {
                overlay.HardHide();
                scene.SetupSceneOverlay();
                return;
            }

            if (key == GameInputKey.Return || key == GameInputKey.Enter)
            {
                var name = PlayerNameFormatter.Normalize(overlay.NameEntryBuffer);
                if (string.IsNullOrEmpty(name))
                {
                    overlay.NameEntryValidationMessage = ">> CALLSIGN CANNOT BE EMPTY";
                    return;
                }

                var priorName = PersistenceSetup.LoadLastPlayerName();
                bool isOwnName = string.Equals(name, priorName, StringComparison.OrdinalIgnoreCase)
                              || PersistenceSetup.HasPlayerSaveFile(name);

                if (!isOwnName)
                {
                    var highscores = HighscoreService.LoadLocalHighscores();
                    bool taken = highscores.Entries.Exists(e =>
                        string.Equals(e.PlayerName, name, StringComparison.OrdinalIgnoreCase));
                    if (taken)
                    {
                        overlay.NameEntryValidationMessage = ">> CALLSIGN ALREADY IN USE - CHOOSE ANOTHER";
                        return;
                    }
                }

                overlay.IsNameConfirmed = true;
                GameState.GamePlayState.PlayerName = name;
                PersistenceSetup.SaveLastPlayerName(name);
                PlayerProgressService.ApplyDurableProgress(GameState.GamePlayState);

                // Check for saved scene progress for this player
                var saved = GameStatePersistence.LoadGameState(name);
                bool shouldStartTutorial = _pendingTutorialStart ||
                                           !TutorialProgressService.HasCompletedTutorial(name);

                if (shouldStartTutorial)
                {
                    _pendingTutorialStart = false;
                    _tutorialResumeSavedState = saved != null && CanResumeSavedSceneAfterTutorial(saved)
                        ? saved
                        : null;
                    _pendingSavedState = null;
                    _targetSceneIndex = GetTutorialSceneIndex();
                }
                else if (saved != null)
                {
                    _tutorialResumeSavedState = null;
                    // Always restore score and stats so the player builds upon them
                    _pendingSavedState = saved;

                    // Skip to saved scene only if it is ahead of the current intro flow
                    if (CanTargetSavedScene(saved))
                    {
                        _targetSceneIndex = ResolveSavedSceneIndex(saved);
                    }
                }

                overlay.HardHide();
                _pendingSceneAdvance = true;
                _pendingSceneAdvanceFramesLeft = SceneAdvanceDelayFrames;
                return;
            }

            overlay.ProcessNameEntryKey(key);
        }

        private static void HandlePlanetLostRecoveryChoice(GameInputKey key, I3dWorld world, ScreenOverlayState overlay)
        {
            if (key == GameInputKey.Up || key == GameInputKey.W || key == GameInputKey.Left || key == GameInputKey.A)
            {
                overlay.MoveChoiceSelection(-1);
                return;
            }

            if (key == GameInputKey.Down || key == GameInputKey.S || key == GameInputKey.Right || key == GameInputKey.D)
            {
                overlay.MoveChoiceSelection(1);
                return;
            }

            if (key == GameInputKey.Escape || key == GameInputKey.X)
            {
                StartPlanetLostRecoveryFade(world, resetToPlanetStart: false);
                return;
            }

            if (key == GameInputKey.Return || key == GameInputKey.Enter || key == GameInputKey.Space)
            {
                StartPlanetLostRecoveryFade(world, resetToPlanetStart: overlay.SelectedChoiceIndex == 1);
            }
        }

        private void HandleSettingsOverlayKey(GameInputKey key, IScene scene, ScreenOverlayState overlay)
        {
            if (key == GameInputKey.Escape || key == GameInputKey.Return || key == GameInputKey.Enter)
            {
                CloseSettingsOverlay(scene, overlay);
                return;
            }

            if (TryOpenSettingsOverlay(key, scene, overlay))
                return;

            if (key == GameInputKey.Up)
            {
                overlay.MoveSettingsSelection(-1, GetSettingsOptionCount(overlay.SettingsPanel));
                RefreshSettingsOverlayBody(overlay);
                return;
            }

            if (key == GameInputKey.Down)
            {
                overlay.MoveSettingsSelection(1, GetSettingsOptionCount(overlay.SettingsPanel));
                RefreshSettingsOverlayBody(overlay);
                return;
            }

            if (key == GameInputKey.Left)
            {
                AdjustSelectedSetting(overlay, -1);
                return;
            }

            if (key == GameInputKey.Right)
            {
                AdjustSelectedSetting(overlay, 1);
            }
        }

        private bool TryOpenSettingsOverlay(GameInputKey key, IScene scene, ScreenOverlayState overlay)
        {
            if (key == GameInputKey.S)
            {
                ShowSettingsOverlay(scene, overlay, ScreenOverlaySettingsPanel.Audio);
                return true;
            }

            if (key == GameInputKey.G)
            {
                ShowSettingsOverlay(scene, overlay, ScreenOverlaySettingsPanel.Graphics);
                return true;
            }

            if (key == GameInputKey.C)
            {
                ShowSettingsOverlay(scene, overlay, ScreenOverlaySettingsPanel.Controls);
                return true;
            }

            return false;
        }

        private void ShowSettingsOverlay(IScene scene, ScreenOverlayState overlay, ScreenOverlaySettingsPanel panel)
        {
            if (scene.SceneType == SceneTypes.Intro && overlay.Type == ScreenOverlayType.Intro)
                _settingsReturnIntroPage = overlay.CurrentPage;

            GameState.SettingsState.Normalize();
            overlay.SetSettingsPreset(
                panel,
                GetSettingsTitle(panel),
                BuildSettingsOverlayBody(panel, selectedIndex: 0),
                GameSettingsOverlayFormatter.Footer);
        }

        private void CloseSettingsOverlay(IScene scene, ScreenOverlayState overlay)
        {
            GameSettingsPersistence.SaveSettings(GameState.SettingsState);
            overlay.HardHide();
            overlay.SettingsPanel = ScreenOverlaySettingsPanel.None;

            if (scene.SceneType == SceneTypes.Intro)
            {
                scene.SetupSceneOverlay();
                overlay.CurrentPage = Math.Clamp(_settingsReturnIntroPage, 0, overlay.TotalPages - 1);
                overlay.ApplyPageContent();
                overlay.ShowOverlay = true;
                return;
            }

            scene.SetupGameOverlay();
        }

        private static void AdjustSelectedSetting(ScreenOverlayState overlay, int direction)
        {
            if (overlay.SettingsPanel == ScreenOverlaySettingsPanel.Audio)
            {
                GameState.SettingsState.AdjustAudio((AudioSettingsField)overlay.SelectedSettingsIndex, direction);
            }
            else if (overlay.SettingsPanel == ScreenOverlaySettingsPanel.Graphics)
            {
                GameState.SettingsState.AdjustGraphics((GraphicsSettingsField)overlay.SelectedSettingsIndex, direction);
            }
            else if (overlay.SettingsPanel == ScreenOverlaySettingsPanel.Controls)
            {
                GameState.SettingsState.AdjustControls(overlay.SelectedSettingsIndex, direction);
            }

            GameSettingsPersistence.SaveSettings(GameState.SettingsState);
            RefreshSettingsOverlayBody(overlay);
        }

        private static void RefreshSettingsOverlayBody(ScreenOverlayState overlay)
        {
            overlay.Body = BuildSettingsOverlayBody(overlay.SettingsPanel, overlay.SelectedSettingsIndex);
        }

        private static string BuildSettingsOverlayBody(ScreenOverlaySettingsPanel panel, int selectedIndex)
        {
            return panel switch
            {
                ScreenOverlaySettingsPanel.Audio => GameSettingsOverlayFormatter.BuildAudioBody(GameState.SettingsState, selectedIndex),
                ScreenOverlaySettingsPanel.Controls => GameSettingsOverlayFormatter.BuildControlsBody(GameState.SettingsState, selectedIndex),
                _ => GameSettingsOverlayFormatter.BuildGraphicsBody(GameState.SettingsState, selectedIndex)
            };
        }

        private static int GetSettingsOptionCount(ScreenOverlaySettingsPanel panel)
        {
            return panel switch
            {
                ScreenOverlaySettingsPanel.Audio => Enum.GetValues<AudioSettingsField>().Length,
                ScreenOverlaySettingsPanel.Controls => GameState.SettingsState.GetControlsOptionCount(),
                _ => Enum.GetValues<GraphicsSettingsField>().Length
            };
        }

        private static string GetSettingsTitle(ScreenOverlaySettingsPanel panel) =>
            panel switch
            {
                ScreenOverlaySettingsPanel.Audio => "SOUND SETTINGS",
                ScreenOverlaySettingsPanel.Controls => "CONTROL SETTINGS",
                _ => "GRAPHICS SETTINGS"
            };

        private static void StartPlanetLostRecoveryFade(I3dWorld world, bool resetToPlanetStart)
        {
            var overlay = GameState.ScreenOverlayState;
            overlay.HardHide();
            overlay.ClearChoiceOptions();

            world.IsPaused = false;
            GameState.GamePlayState.Phase = GamePhase.Playing;
            GameState.WorldFade.RequestFadeOut(
                1.0f,
                resetToPlanetStart
                    ? WorldFadeState.InfectionCriticalPlanetResetReason
                    : WorldFadeState.InfectionCriticalContinueReason);
        }

        private void HandleIntroKey(ScreenOverlayState overlay, GameInputKey key)
        {
            if (Logger.ShouldLog(enableLogging)) Logger.Log($"Scenehandler: Keypress during Intro ShowOverlay: {overlay.ShowOverlay} ", "General");

            if (IsTutorialStartKey(key))
            {
                _pendingTutorialStart = true;
                ShowNameEntryOverlay(overlay);
                return;
            }

            // Page navigation with arrow keys
            if (overlay.HasMultiplePages)
            {
                if (key == GameInputKey.Right || key == GameInputKey.D)
                {
                    overlay.NextPage();
                    RefreshCurrentHighscorePage(overlay);
                    return;
                }
                if (key == GameInputKey.Left || key == GameInputKey.A)
                {
                    overlay.PreviousPage();
                    RefreshCurrentHighscorePage(overlay);
                    return;
                }
            }

            ShowNameEntryOverlay(overlay);
        }

        private static void HandleGameKey(GameInputKey key, IScene scene, ScreenOverlayState overlay)
        {
            if (overlay.Type == ScreenOverlayType.Intro && overlay.ShowOverlay)
            {
                // Page navigation with arrow keys
                if (overlay.HasMultiplePages)
                {
                    if (key == GameInputKey.Right || key == GameInputKey.D)
                    {
                        overlay.NextPage();
                        RefreshCurrentHighscorePage(overlay);
                        return;
                    }
                    if (key == GameInputKey.Left || key == GameInputKey.A)
                    {
                        overlay.PreviousPage();
                        RefreshCurrentHighscorePage(overlay);
                        return;
                    }
                }

                if (Logger.ShouldLog(enableLogging)) Logger.Log($"Scenehandler: Game keypress. Overlay Type={overlay.Type} Show={overlay.ShowOverlay}", "General");
                scene.SetupGameOverlay();
            }
        }

        private static void RefreshCurrentHighscorePage(ScreenOverlayState overlay)
        {
            HighscoreOverlayFormatter.RefreshCurrentPageIfHighscorePage(overlay);
        }

        private static bool IsMenuExitKey(GameInputKey key) => key == GameInputKey.X || key == GameInputKey.Escape;
        private static bool IsTutorialStartKey(GameInputKey key) => key == GameInputKey.T;
        private static bool CanOpenSettingsOverlay(IScene scene, ScreenOverlayState overlay)
        {
            if (scene.SceneType == SceneTypes.Intro && overlay.ShowOverlay && overlay.Type == ScreenOverlayType.Intro)
                return true;

            return (scene.SceneType == SceneTypes.Game || scene.SceneType == SceneTypes.Simulation) &&
                   GameState.GamePlayState.IsPaused;
        }

        // -----------------------------------------------------------------
        // Shared helpers
        // -----------------------------------------------------------------

        private void ShowNameEntryOverlay(ScreenOverlayState overlay)
        {
            overlay.HardHide();
            ClearVideoOverlay();

            var lastPlayer = PersistenceSetup.LoadLastPlayerName();
            overlay.SetNameEntryPreset(lastPlayer);
        }

        private static void CloseTutorialOverlayAndResume(IScene scene, I3dWorld world)
        {
            ShipAiVoiceService.Shared.StopCurrentSpeech();
            GameState.TutorialState.ClearInstructionOverlay();
            scene.SetupGameOverlay();
            RestoreTutorialShipAfterOverlayPause(world);
            world.IsPaused = false;
        }

        private static void RestoreTutorialShipAfterOverlayPause(I3dWorld world)
        {
            for (int i = 0; i < world.WorldInhabitants.Count; i++)
            {
                var obj = world.WorldInhabitants[i];
                if (obj.ObjectName == "Ship" && obj.Movement is ShipControls shipControls)
                {
                    shipControls.RestoreOverlayPauseTransformAndSuppressCrashDetection(obj);
                    return;
                }
            }
        }

        private static void SkipLogoCube(I3dWorld world, IScene scene)
        {
            var inhabitants = world.WorldInhabitants;
            for (int i = inhabitants.Count - 1; i >= 0; i--)
            {
                if (inhabitants[i].ObjectName == "LogoCube")
                {
                    inhabitants.RemoveAt(i);
                    break;
                }
            }

            GameState.ScreenOverlayState.ShowOverlay = true;
            scene.SetupVideoOverlay("introclip.mp4");
        }

        private bool CanTargetSavedScene(SavedGameState saved)
        {
            int savedSceneIndex = ResolveSavedSceneIndex(saved);
            if (savedSceneIndex <= currentSceneIndex + 1 || savedSceneIndex >= scenes.Count)
                return false;

            var sceneType = scenes[savedSceneIndex].SceneType;
            return sceneType == SceneTypes.Game ||
                   sceneType == SceneTypes.Outro ||
                   sceneType == SceneTypes.Simulation;
        }

        private bool CanResumeSavedSceneAfterTutorial(SavedGameState saved)
        {
            int savedSceneIndex = ResolveSavedSceneIndex(saved);
            if (savedSceneIndex < 0 || savedSceneIndex >= scenes.Count)
                return false;

            var sceneType = scenes[savedSceneIndex].SceneType;
            return sceneType == SceneTypes.Game ||
                   sceneType == SceneTypes.Outro ||
                   sceneType == SceneTypes.Simulation;
        }

        private int ResolveSavedSceneIndex(SavedGameState saved)
        {
            if (saved.HasCheckpoint && saved.CheckpointSceneIndex > 0)
                return saved.CheckpointSceneIndex;

            if (LooksLikeLegacySimulationCheckpoint(saved))
            {
                int simulationIndex = scenes.FindIndex(s => s.SceneType == SceneTypes.Simulation);
                if (simulationIndex >= 0)
                    return simulationIndex;
            }

            return saved.SceneIndex;
        }

        private static bool LooksLikeLegacySimulationCheckpoint(SavedGameState saved)
        {
            if (!saved.HasCheckpoint || saved.SimulationRound <= 0)
                return false;

            return saved.CheckpointSimulationRound > 0 ||
                   saved.SceneBiome != SceneBiomeTypes.HillsWoods ||
                   saved.CheckpointSceneBiome != SceneBiomeTypes.HillsWoods;
        }

        private int GetTutorialSceneIndex()
        {
            int tutorialIndex = scenes.FindIndex(s => s.SceneType == SceneTypes.Tutorial);
            return tutorialIndex >= 0 ? tutorialIndex : 1;
        }

        private int GetFirstGameSceneIndex()
        {
            int gameIndex = scenes.FindIndex(s => s.SceneType == SceneTypes.Game);
            return gameIndex >= 0 ? gameIndex : 1;
        }

        private int ApplyTutorialGate(int candidateSceneIndex)
        {
            if (candidateSceneIndex < 0 || candidateSceneIndex >= scenes.Count)
                return candidateSceneIndex;

            var candidate = scenes[candidateSceneIndex];
            if (candidate.SceneType != SceneTypes.Game && candidate.SceneType != SceneTypes.Simulation)
                return candidateSceneIndex;

            var playerName = GameState.GamePlayState.PlayerName;
            if (string.IsNullOrWhiteSpace(playerName))
                return candidateSceneIndex;

            return TutorialProgressService.HasCompletedTutorial(playerName)
                ? candidateSceneIndex
                : GetTutorialSceneIndex();
        }

        private IScene? CreateFreshScene() =>
            (IScene?)Activator.CreateInstance(GetActiveScene().GetType());

        private void ReturnToIntro(I3dWorld world)
        {
            ReturnToIntro(world, persistCurrentRun: true);
        }

        private void ReturnToIntro(I3dWorld world, bool persistCurrentRun)
        {
            DisposeDirector();
            var returningFromTutorial = GetActiveScene().SceneType == SceneTypes.Tutorial;
            var gps = GameState.GamePlayState;
            ShipAiVoiceService.Shared.StopCurrentSpeech();
            if (returningFromTutorial)
            {
                TutorialProgressService.MarkTutorialCompleted(gps.PlayerName);
                _tutorialResumeSavedState = null;
                _tutorialEntrySnapshot = null;
            }

            // Returning to intro is a menu transition only. Durable progress and
            // highscores are written by checkpoint flows: powerups and motherships.

            // Clear all game objects so nothing from the current scene bleeds through
            DisposeWorldMovements(world);
            world.WorldInhabitants.Clear();
            if (GameState.SurfaceState.AiObjects != null)
                GameState.SurfaceState.AiObjects.Clear();

            currentSceneIndex = 0;
            ClearVideoOverlay();
            gps.ResetForNewGame();
            gps.SceneIndex = 0;
            ResetSurfaceState();

            // Replace the intro scene instance so SkipLogoCube is set before SetupScene runs
            var introScene = new Intro { SkipLogoCube = true };
            scenes[0] = introScene;

            introScene.SetupSceneOverlay();
            ApplySceneSettings(introScene);
            introScene.SetupScene((GameWorld)world);
        }

        private static void DisposeWorldMovements(I3dWorld world)
        {
            foreach (var obj in world.WorldInhabitants.OfType<_3dObject>())
            {
                try
                {
                    obj.Movement?.Dispose();
                }
                catch (NotImplementedException)
                {
                    // Some passive movements still use the legacy no-op contract.
                }
            }
        }

        private static void ClearWorldRuntimeState(I3dWorld world)
        {
            DisposeWorldMovements(world);
            world.WorldInhabitants.Clear();
            if (GameState.SurfaceState.AiObjects != null)
                GameState.SurfaceState.AiObjects.Clear();
        }

        private static void ClearVideoOverlay()
        {
            GameState.ScreenOverlayState.ShowVideoOverlay = false;
            GameState.ScreenOverlayState.VideoClipPath = string.Empty;
        }

        private static void ResetSurfaceState()
        {
            GameState.SurfaceState.GlobalMapPixels = null;
            GameState.SurfaceState.SurfaceViewportObject = null;
            GameState.SurfaceState.GlobalMapPosition = new Vector3
            {
                x = SurfaceSetup.DefaultMapPosition.x,
                y = SurfaceSetup.DefaultMapPosition.y,
                z = SurfaceSetup.DefaultMapPosition.z
            };
            GameState.SurfaceState.ScreenEcoMetas = new ScreenEcoMeta[MapSetup.screensPrMap, MapSetup.screensPrMap];
            GameState.SurfaceState.SceneBiome = SceneBiomeTypes.HillsWoods;
            GameState.WeatherVisualState.ClearLightningFlash();
        }

        private static void ApplySceneSettings(IScene scene)
        {
            var gps = GameState.GamePlayState;
            gps.InfectionCriticalMass = scene.InfectionThresholdPercent;
            gps.InfectionSpreadRate = scene.InfectionSpreadRate;
            gps.SeederOffscreenSpeedFactor = scene.SeederOffscreenSpeedFactor;
            gps.LocalInfectionSpreadDelaySec = scene.LocalInfectionSpreadDelaySec;
            gps.LocalInfectionSpreadRadius = scene.LocalInfectionSpreadRadius;
            gps.MotherShipSmallAggression = scene.MotherShipSmallAggression;
            gps.MotherShipMediumAggression = scene.MotherShipMediumAggression;
            gps.MotherShipLargeAggression = scene.MotherShipLargeAggression;
            gps.CurrentSceneType = scene.SceneType;
            gps.CurrentSceneBiome = scene.SceneBiome;
            GameState.SurfaceState.SceneBiome = scene.SceneBiome;
        }

        private void InitializeDirector(IScene scene, I3dWorld world)
        {
            scene.Director?.Initialize(world.EventBus!, world);
        }

        private void DisposeDirector()
        {
            GetActiveScene().Director?.Dispose();
        }

        private void ApplySceneIndexOverrideFromGameState()
        {
            var gamePlayState = GameState.GamePlayState;
            if (gamePlayState == null || !CanUseSceneIndexAsStartupOverride(gamePlayState))
                return;

            var requestedSceneIndex = gamePlayState.SceneIndex;
            if (requestedSceneIndex < 0 || requestedSceneIndex >= scenes.Count)
                return;

            currentSceneIndex = requestedSceneIndex;
        }

        private static bool CanUseSceneIndexAsStartupOverride(GamePlayState gamePlayState)
        {
            return string.IsNullOrWhiteSpace(gamePlayState.PlayerName)
                && gamePlayState.Score == 0
                && gamePlayState.TotalKills == 0
                && gamePlayState.TotalShotsFired == 0
                && gamePlayState.TotalDeaths == 0
                && gamePlayState.PowerUpsCollected == 0
                && gamePlayState.SpeedPowerUpLevel == 0
                && !gamePlayState.HasCheckpoint;
        }

        // -----------------------------------------------------------------
        // Enemy trimming (checkpoint restore)
        // -----------------------------------------------------------------

        /// <summary>
        /// Removes excess enemies of a given type so the count matches the checkpoint
        /// remaining count. Removes non-powerup enemies first.
        /// Drones and MotherShips are only counted when active (matching UpdateHudState).
        /// </summary>
        private static void TrimEnemies(I3dWorld world, string enemyName, int targetRemaining)
        {
            var inhabitants = world.WorldInhabitants;

            int current = 0;
            for (int i = 0; i < inhabitants.Count; i++)
            {
                if (inhabitants[i].ObjectName == enemyName)
                    current++;
            }

            int toRemove = current - targetRemaining;
            if (toRemove <= 0) return;

            // First pass: non-powerup enemies (from end)
            for (int i = inhabitants.Count - 1; i >= 0 && toRemove > 0; i--)
            {
                var obj = inhabitants[i];
                if (obj.ObjectName == enemyName && !obj.HasPowerUp)
                {
                    RemoveEnemyAt(world, i, obj.ObjectId);
                    toRemove--;
                }
            }

            // Second pass: powerup enemies if still needed
            for (int i = inhabitants.Count - 1; i >= 0 && toRemove > 0; i--)
            {
                var obj = inhabitants[i];
                if (obj.ObjectName == enemyName && obj.HasPowerUp)
                {
                    RemoveEnemyAt(world, i, obj.ObjectId);
                    toRemove--;
                }
            }
        }

        private static void RemoveEnemyAt(I3dWorld world, int inhabitantIndex, int objectId)
        {
            world.WorldInhabitants.RemoveAt(inhabitantIndex);

            var aiObjects = GameState.SurfaceState.AiObjects;
            for (int j = aiObjects.Count - 1; j >= 0; j--)
            {
                if (aiObjects[j].ObjectId == objectId)
                {
                    aiObjects.RemoveAt(j);
                    break;
                }
            }
        }

        private static bool SavedCheckpointMatchesActiveScene(SavedGameState saved)
        {
            int sceneSeeders = CountSceneAi("Seeder");
            int sceneDrones = CountSceneAi("KamikazeDrone");
            int sceneMotherShips = CountSceneMotherShips();

            if (!CheckpointInitialMatchesScene(saved.CheckpointInitialSeeders, sceneSeeders))
                return false;
            if (!CheckpointInitialMatchesScene(saved.CheckpointInitialDrones, sceneDrones))
                return false;
            if (!CheckpointInitialMatchesScene(saved.CheckpointInitialMotherShips, sceneMotherShips))
                return false;

            return true;
        }

        private static bool CheckpointInitialMatchesScene(int checkpointInitial, int sceneInitial)
        {
            return checkpointInitial <= 0 || sceneInitial <= 0 || checkpointInitial == sceneInitial;
        }

        private static bool IsSceneBoundarySaveTarget(IScene scene)
        {
            return scene.SceneType == SceneTypes.Game ||
                   scene.SceneType == SceneTypes.Outro ||
                   scene.SceneType == SceneTypes.Simulation;
        }

        private static void PersistSceneBoundaryProgress(GamePlayState gps)
        {
            // Scene completion starts the player on the next playable scene, but it
            // must not carry an old in-scene checkpoint into the new scene. The AI
            // save-confirmation voice is intentionally NOT requested here: scene
            // boundaries are not player-driven save events, so the voice would feel
            // disconnected from any in-game action. The voice still plays for the
            // in-scene saves triggered by powerup pickup (ShipControls.CollectPowerUp)
            // and mothership kills (LiveGameLoop.CleanupExplodedObjects).
            ClearCheckpointState(gps);

            try
            {
                GameStatePersistence.SaveGameState();
            }
            catch { }
            try { HighscoreService.SubmitFromGamePlay(gps); } catch { }
        }

        private static void PublishSceneCompleted(
            I3dWorld world,
            GamePlayState gps,
            int completedSceneIndex,
            SceneTypes completedSceneType)
        {
            world.EventBus?.Publish(new GameEvent
            {
                Type = GameEventType.SceneCompleted,
                ObjectName = completedSceneType.ToString(),
                SceneType = completedSceneType,
                SceneIndex = completedSceneIndex,
                Score = gps.Score,
                TotalKills = gps.TotalKills,
                TotalShotsFired = gps.TotalShotsFired,
                TotalDeaths = gps.TotalDeaths,
                Accuracy = gps.Accuracy,
                PowerUpsCollected = gps.PowerUpsCollected,
                SpeedPowerUpLevel = gps.SpeedPowerUpLevel
            });
        }

        private static void CapturePlanetStartSnapshotIfNeeded(IScene scene)
        {
            if (scene.SceneType != SceneTypes.Game && scene.SceneType != SceneTypes.Simulation)
                return;

            var gps = GameState.GamePlayState;
            if (gps.HasPlanetStartSnapshot && gps.PlanetStartSceneIndex == gps.SceneIndex)
                return;

            SyncGameplayEnemyCountsFromScene(resetInitialCounts: true);
            gps.SavePlanetStartSnapshot();
        }

        private static void SyncGameplayEnemyCountsFromScene(bool resetInitialCounts)
        {
            var gps = GameState.GamePlayState;
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null)
                return;

            int seeders = 0;
            int drones = 0;
            int motherShips = 0;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i];
                if (obj.ImpactStatus?.HasExploded == true)
                    continue;

                if (obj.ObjectName == "Seeder")
                    seeders++;
                else if (obj.ObjectName == "KamikazeDrone" && obj.IsActive)
                    drones++;
                else if (IsMotherShipName(obj.ObjectName) && obj.IsActive)
                    motherShips++;
            }

            gps.SeedersRemaining = seeders;
            gps.DronesRemaining = drones;
            gps.MotherShipsRemaining = motherShips;

            if (resetInitialCounts || gps.InitialSeeders == 0)
                gps.InitialSeeders = seeders;
            if (resetInitialCounts || gps.InitialDrones == 0)
                gps.InitialDrones = drones;
            if (resetInitialCounts || gps.InitialMotherShips == 0)
                gps.InitialMotherShips = motherShips;
        }

        private static int CountSceneAi(string objectName)
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null) return 0;

            int count = 0;
            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (aiObjects[i].ObjectName == objectName)
                    count++;
            }

            return count;
        }

        private static int ResolveRestoredMotherShipCount(int checkpointMotherShipsRemaining)
        {
            int sceneMotherShips = CountSceneMotherShips();
            if (sceneMotherShips <= 0)
                return 0;

            if (checkpointMotherShipsRemaining > 0)
                return Math.Min(checkpointMotherShipsRemaining, sceneMotherShips);

            // Keep scene mothership candidates even when old checkpoint saves have 0 remaining,
            // otherwise late-phase activation can never happen.
            return sceneMotherShips;
        }

        private static int CountSceneMotherShips()
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null) return 0;

            int count = 0;
            for (int i = 0; i < aiObjects.Count; i++)
            {
                var objectName = aiObjects[i].ObjectName;
                if (objectName == "MotherShipSmall" ||
                    objectName == "MotherShipMedium" ||
                    objectName == "MotherShipLarge")
                    count++;
            }

            return count;
        }

        private static void ActivateMotherShips(I3dWorld world)
        {
            for (int i = 0; i < world.WorldInhabitants.Count; i++)
            {
                if (IsMotherShipName(world.WorldInhabitants[i].ObjectName))
                    world.WorldInhabitants[i].IsActive = true;
            }

            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null)
                return;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                if (IsMotherShipName(aiObjects[i].ObjectName))
                    aiObjects[i].IsActive = true;
            }
        }

        private static bool IsMotherShipName(string? objectName)
        {
            return objectName == "MotherShipSmall" ||
                   objectName == "MotherShipMedium" ||
                   objectName == "MotherShipLarge";
        }

        private static void ClearCheckpointState(GamePlayState gps)
        {
            gps.ClearCheckpoint();
        }

        private static void ApplyCheckpointSnapshotToCurrentState(
            GamePlayState gps,
            GamePlayState.CheckpointSnapshot snapshot)
        {
            gps.Score = snapshot.Score;
            gps.Lives = snapshot.Lives;
            gps.Health = snapshot.Health;
            gps.PowerUpsCollected = snapshot.PowerUpsCollected;
            gps.SpeedPowerUpLevel = snapshot.SpeedPowerUpLevel;
            gps.SeedersRemaining = snapshot.SeedersRemaining;
            gps.DronesRemaining = snapshot.DronesRemaining;
            gps.MotherShipsRemaining = snapshot.MotherShipsRemaining;
            gps.TotalShotsFired = snapshot.TotalShotsFired;
            gps.TotalKills = snapshot.TotalKills;
            gps.TotalDeaths = snapshot.TotalDeaths;
            gps.InfectionLevel = snapshot.InfectionLevel;
            gps.WaveNumber = snapshot.WaveNumber;
            gps.SceneIndex = snapshot.SceneIndex;
            gps.SimulationRound = snapshot.SimulationRound;
            gps.CurrentSceneBiome = snapshot.SceneBiome;
            gps.InitialSeeders = snapshot.InitialSeeders;
            gps.InitialDrones = snapshot.InitialDrones;
            gps.InitialMotherShips = snapshot.InitialMotherShips;
            gps.PlanetStyleBonusScore = snapshot.PlanetStyleBonusScore;
            gps.PlanetStyleBonusSceneIndex = snapshot.PlanetStyleBonusSceneIndex;
        }

        // -----------------------------------------------------------------
        // Validation (DEBUG only)
        // -----------------------------------------------------------------

        [Conditional("DEBUG")]
        private static void ValidateGameSceneSetup(IScene scene, I3dWorld world)
        {
            if (scene.SceneType != SceneTypes.Game) return;

            var inhabitants = world.WorldInhabitants;
            var ship = inhabitants.FirstOrDefault(o => o.ObjectName == "Ship");

            Debug.Assert(ship != null,
                $"[{scene.GetType().Name}] No Ship found in WorldInhabitants.");
            Debug.Assert(ship?.WeaponSystems != null,
                $"[{scene.GetType().Name}] Ship.WeaponSystems is null — weapons not assigned.");
            Debug.Assert(ship?.ImpactStatus?.ObjectHealth > 0,
                $"[{scene.GetType().Name}] Ship.ImpactStatus.ObjectHealth is 0 — ship has no health.");
            Debug.Assert(GameState.SurfaceState.SurfaceViewportObject != null,
                $"[{scene.GetType().Name}] SurfaceViewportObject is null — surface not stored in GameState.");
            Debug.Assert(GameState.SurfaceState.AiObjects.Count > 0,
                $"[{scene.GetType().Name}] No AI objects — scene has no enemies.");
            Debug.Assert(inhabitants.Any(o => o.ObjectName == "SeederGuidanceArrow"),
                $"[{scene.GetType().Name}] No SeederGuidanceArrow — guidance arrow missing.");
        }
    }
}
