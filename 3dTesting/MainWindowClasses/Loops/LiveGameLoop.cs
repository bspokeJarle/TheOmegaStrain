using _3dRotations.Helpers;
using _3dRotations.World.Objects;
using _3dTesting._3dRotation;
using _3dTesting._Coordinates;
using _3dTesting.Helpers;
using CommonUtilities._3DHelpers;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.Events;
using CommonUtilities.GamePlayHelpers;
using CommonUtilities.Persistence;
using Domain;
using GameAiAndControls.Audio.Services;
using GameAiAndControls.Controls;
using GameAiAndControls.Controls.SeederControls;
using GameAudioInstances;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static Domain._3dSpecificsImplementations;

namespace _3dTesting.MainWindowClasses.Loops
{
    public class LiveGameLoop : IGameLoop<_2dTriangleMesh>
    {
        public const bool EnableCpuHeadroomLogging = false;
        private const bool EnableAdaptiveGc = true;
        private static int PerfLogInterval => ScreenSetup.RuntimeTargetFps;
        private static int AdaptiveGcMinFrameInterval => ScreenSetup.RuntimeTargetFps;
        private const int adaptiveGcGen1EveryAttempts = 6;
        private const double adaptiveGcMinHeadroomMs = 5.0;
        private const double adaptiveGcMinHeadroomPct = 45.0;
        private const long adaptiveGcMinAllocatedBytes = 24L * 1024L * 1024L;

        private long FrameCounter = 0;
        private readonly Stopwatch frameTimer = new();
        private long performanceFrameCount = 0;
        private double averageFrameMs = 0;
        private double averageHeadroomMs = 0;
        private long lastAdaptiveGcFrame = -AdaptiveGcMinFrameInterval;
        private long lastAdaptiveGcAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
        private long adaptiveGcAttempts = 0;
        private int AiUpdateCounter = 0;
        private const int AiUpdateInterval = 5; // Update offscreen AI every 5 frames
        private readonly _3dTo2d From3dTo2d = new();
        private readonly _3dRotationCommon Rotate3d = new();
        private readonly ParticleManager particleManager = new();
        private readonly WeaponsManager weaponsManager = new();
        private readonly ObjectShadowManager objectShadowManager = new();
        private readonly List<_3dObject> activeWorldBuffer = new();
        private readonly List<_3dObject> deepCopiedWorldBuffer = new();
        private readonly List<_3dObject> particleObjectBuffer = new();
        private readonly List<_3dObject> weaponObjectBuffer = new();
        private readonly List<_3dObject> shadowObjectBuffer = new();
        private readonly List<_3dObject> renderedObjectBuffer = new();
        private readonly Dictionary<int, _3dObject> aiByIdBuffer = new();
        private readonly HashSet<int> pendingExplosionCleanupIds = new();
        private readonly HashSet<int> publishedExplosionIds = new();
        private IGameEventBus? explosionCleanupEventBus;
        private StarFieldHandler StarFieldHandler { get; set; }

        private const float DefaultMusicVolume = 0.15f;
        private readonly IAudioPlayer audioPlayer = new NAudioAudioPlayer(AudioSetup.AudioBasePath);
        private readonly ISoundRegistry soundRegistry = new JsonSoundRegistry(AudioSetup.SoundRegistryPath);
        private SoundDefinition MusicDef { get; set; } = null;
        private bool MusicIsPlaying { get; set; } = false;
        private string CurrentSceneMusicId { get; set; } = string.Empty;
        private long AppliedAudioSettingsVersion { get; set; } = -1;
        private const string BiomassCriticalWarningSoundId = "biomass_critical_warning";
        private const string BiomassAbortWarningSoundId = "biomass_abort_warning";
        private bool _biomassCriticalWarningPlayed = false;
        private bool _biomassAbortWarningPlayed = false;

        public string DebugMessage { get; set; }
        private bool enableLocalLogging = false;
        private const bool enableProgressionLogging = false;
        public bool FadeOutWorld
        {
            get => GameState.WorldFade.IsFadeOutPendingOrActive;
            set
            {
                if (value)
                    GameState.WorldFade.RequestFadeOut(1.0f, "LegacyFadeOutWorld");
            }
        }

        public bool FadeInWorld
        {
            get => GameState.WorldFade.IsFadeInPendingOrActive;
            set
            {
                if (value)
                    GameState.WorldFade.RequestFadeIn(1.5f, "LegacyFadeInWorld");
            }
        }

        public bool SceneResetReady
        {
            get => GameState.WorldFade.IsBlack;
            set
            {
                if (value)
                    GameState.WorldFade.MarkFadeOutComplete();
            }
        }
        private bool _deathSequenceStarted = false;
        private bool _victorySequenceStarted = false;
        private long _victoryStartTicks = 0;
        private const float VictoryDisplaySeconds = 3.0f;
        private const float VictoryRewardCountUpSeconds = 4.0f;
        private const float VictoryRewardHoldSeconds = 1.25f;
        private PlanetRewardBreakdown? _victoryRewardBreakdown;
        private int _lastAppliedVictoryReward;
        private static readonly HashSet<Type> movementDisposeNotImplementedTypes = new();
        private static readonly object movementDisposeNotImplementedTypesLock = new();

        private readonly object _lock = new object();
        public I3dObject ShipCopy { get; set; }
        public I3dObject SurfaceCopy { get; set; }

        public List<_2dTriangleMesh> UpdateWorld(I3dWorld world, ref List<_2dTriangleMesh> projectedCoordinates, ref List<_2dTriangleMesh> crashBoxCoordinates)
        {
            frameTimer.Restart();
            FrameCounter++;
            GameState.WeatherVisualState.DecayImpactFlash(3.2f * GameState.ClampedDeltaTime);
            EnsureExplosionCleanupSubscription(world.EventBus);
            bool logPhaseTiming = Logger.ShouldLog(EnableCpuHeadroomLogging) && (FrameCounter % PerfLogInterval == 0);
            long phaseTicks = logPhaseTiming ? Stopwatch.GetTimestamp() : 0;
            double copyMs = 0;
            double starfieldMs = 0;
            double prepMs = 0;
            double moveRotateMs = 0;
            double mergeMs = 0;
            double offscreenAiMs = 0;
            double infectionMs = 0;
            double projectMs = 0;
            double crashMs = 0;
            double cleanupMs = 0;
            double directorHudMs = 0;
            double musicMs = 0;

            double MarkPhase()
            {
                if (!logPhaseTiming)
                    return 0;

                long now = Stopwatch.GetTimestamp();
                double elapsedMs = TicksToMs(now - phaseTicks);
                phaseTicks = now;
                return elapsedMs;
            }

            List<_3dObject> deepCopiedWorld = deepCopiedWorldBuffer;
            List<_3dObject> activeWorld = activeWorldBuffer;
            lock (_lock)
            {
                if (GameState.PendingWorldObjects.Count > 0)
                {
                    world.WorldInhabitants.AddRange(GameState.PendingWorldObjects);
                    GameState.PendingWorldObjects.Clear();
                }

                var inhabitants = world.WorldInhabitants;
                activeWorld.Clear();
                EnsureListCapacity(activeWorld, inhabitants.Count);

                foreach (var inhabitant in inhabitants)
                {
                    if (inhabitant.ObjectParts.Count == 0) continue;
                    if (!inhabitant.IsActive) continue;

                    if (inhabitant is _3dObject concreteInhabitant && concreteInhabitant.CheckInhabitantVisibility())
                    {
                        activeWorld.Add(concreteInhabitant);
                    }
                }

                Common3dObjectHelpers.DeepCopy3dObjects(activeWorld, deepCopiedWorld);
            }
            copyMs = MarkPhase();

            if (StarFieldHandler == null)
            {
                var parentSurface = world.WorldInhabitants?.FirstOrDefault(obj => obj.ObjectName == "Surface")?.ParentSurface;
                if (parentSurface != null)
                {
                    StarFieldHandler = new StarFieldHandler(parentSurface);
                }
            }
            if (StarFieldHandler != null)
            {
                StarFieldHandler.GenerateStarfield();
                if (StarFieldHandler.HasStars()) deepCopiedWorld.AddRange(StarFieldHandler.GetStars());
                float weatherOpacity = GetWeatherEffectsOpacity(1f - StarFieldHandler.PoolOpacity);
                SnowfallControls.GlobalSnowOpacity = weatherOpacity;
                RainfallControls.GlobalRainOpacity = weatherOpacity;
                SandDriftControls.GlobalSandOpacity = weatherOpacity;
                LeafDriftControls.GlobalLeafOpacity = weatherOpacity;
            }
            starfieldMs = MarkPhase();

            var particleObjectList = particleObjectBuffer;
            var weaponObjectList = weaponObjectBuffer;
            var shadowObjectList = shadowObjectBuffer;
            var renderedList = renderedObjectBuffer;
            particleObjectList.Clear();
            weaponObjectList.Clear();
            shadowObjectList.Clear();
            renderedList.Clear();
            EnsureListCapacity(renderedList, deepCopiedWorld.Count);
            DebugMessage = string.Empty;

            AiUpdateCounter++;
            bool doAiMark = AiUpdateCounter >= AiUpdateInterval;
            if (doAiMark) AiUpdateCounter = 0;

            Dictionary<int, _3dObject> aiById = null;
            if (doAiMark)
            {
                aiById = InitializeAiOnScreenTracking();
            }
            prepMs = MarkPhase();

            foreach (var inhabitant in deepCopiedWorld)
            {
                if (inhabitant.ObjectName != "Star" && !inhabitant.CheckInhabitantVisibility()) continue;
                inhabitant.IsOnScreen = true;
                if (doAiMark)
                {
                    SetAiIsOnScreen(aiById, inhabitant.ObjectId);
                }

                inhabitant.Movement?.MoveObject(inhabitant, audioPlayer, soundRegistry);
                PublishObjectExplodedIfNeeded(world, inhabitant);
                if (inhabitant.CrashBoxesFollowRotation) inhabitant.CrashBoxes = RotateAllCrashboxes(inhabitant.CrashBoxes, (Vector3)inhabitant.Rotation);
                if (inhabitant.ObjectName == "Ship")
                {
                    ShipCopy = inhabitant;
                }
                if (inhabitant.ObjectName == "Surface")
                {
                    SurfaceCopy = inhabitant;
                }

                foreach (var part in inhabitant.ObjectParts)
                {
                    part.Triangles = RotateMesh(part.Triangles, (Vector3)inhabitant.Rotation);

                    if (inhabitant.ObjectName == "Surface")
                    {
                        inhabitant.ParentSurface.RotatedSurfaceTriangles = part.Triangles;

                        var landBasedIds = inhabitant.ParentSurface.LandBasedIds;
                        landBasedIds.Clear();

                        var triangleByLandId = inhabitant.ParentSurface.RotatedSurfaceTriangleByLandId;
                        triangleByLandId.Clear();

                        foreach (var triangle in part.Triangles)
                        {
                            var landBasedPosition = triangle.landBasedPosition;
                            landBasedIds.Add(landBasedPosition);

                            if (landBasedPosition.HasValue)
                                triangleByLandId[landBasedPosition.Value] = triangle;
                        }
                    }

                    SetMovementGuides(inhabitant, part, part.Triangles);
                }

                if (inhabitant.ObjectName == "Surface")
                    DebugMessage += $" Surface: Y Pos: {inhabitant.ObjectOffsets.y}";

                if (inhabitant.ObjectName == "Ship")
                    DebugMessage += $" Ship: Y Pos: {inhabitant.ObjectOffsets.y + 300} Z Rotation: {inhabitant.Rotation.z}";

                particleManager.HandleParticles(inhabitant, particleObjectList);
                weaponsManager.HandleWeapons(inhabitant, weaponObjectList);
                if (GameState.SettingsState.EnhancedShadowsEnabled)
                    objectShadowManager.HandleObjectShadow(inhabitant, shadowObjectList);
                renderedList.Add(inhabitant);

            }
            moveRotateMs = MarkPhase();

            if (shadowObjectList.Count > 0)
            {
                renderedList.InsertRange(0, shadowObjectList);
            }
            if (particleObjectList.Count > 0)
            {
                renderedList.AddRange(particleObjectList);
                DebugMessage += $" Number of Particles on screen {particleObjectList.Count}";
            }
            if (weaponObjectList.Count > 0)
            {
                renderedList.AddRange(weaponObjectList);
                DebugMessage += $" Number of Weapons on screen {weaponObjectList.Count}";
            }
            mergeMs = MarkPhase();

            var activeScene = world.SceneHandler.GetActiveScene();

            HandleBiomassWarnings();

            var ship = activeWorld.FirstOrDefault(x => x.ObjectName == "Ship");
            if (ship != null && ship.ImpactStatus.HasExploded && !_deathSequenceStarted)
            {
                _deathSequenceStarted = true;
                _victorySequenceStarted = false;
                GameState.WorldFade.RequestFadeOut(1.0f, WorldFadeState.ShipDestroyedReason);
            }

            if (!_deathSequenceStarted &&
                GameState.GamePlayState.IsInfectionCritical &&
                !GameState.WorldFade.IsFadeOutPendingOrActive)
            {
                ShowPlanetLostChoiceOverlay(world);
            }

            bool isChoiceDrivenInfectionReset =
                GameState.WorldFade.IsBlack &&
                (GameState.WorldFade.Reason == WorldFadeState.InfectionCriticalContinueReason ||
                 GameState.WorldFade.Reason == WorldFadeState.InfectionCriticalPlanetResetReason);

            if ((_deathSequenceStarted || _victorySequenceStarted || isChoiceDrivenInfectionReset) && GameState.WorldFade.IsBlack)
            {
                StopNonMusicAudio();
                CleanupWorldObjects(world.WorldInhabitants.OfType<_3dObject>().ToList());
                world.WorldInhabitants.Clear();
                GameState.SurfaceState.AiObjects.Clear();
                GameState.SurfaceState.DirtyTiles.Clear();
                GameState.SurfaceState.PendingLocalInfectionSpread.Clear();
                GameState.ShipState.BestCandidateStates.Clear();
                pendingExplosionCleanupIds.Clear();
                publishedExplosionIds.Clear();
                StarFieldHandler.ClearStars();
                StarFieldHandler = null;
                float weatherOpacity = GetWeatherEffectsOpacity(1f);
                SnowfallControls.GlobalSnowOpacity = weatherOpacity;
                RainfallControls.GlobalRainOpacity = weatherOpacity;
                SandDriftControls.GlobalSandOpacity = weatherOpacity;
                LeafDriftControls.GlobalLeafOpacity = weatherOpacity;

                if (_victorySequenceStarted && !_deathSequenceStarted)
                    world.SceneHandler.NextScene(world);
                else if (GameState.WorldFade.Reason == WorldFadeState.InfectionCriticalPlanetResetReason)
                    world.SceneHandler.ResetActiveSceneToPlanetStart(world);
                else
                    world.SceneHandler.ResetActiveScene(world);

                _deathSequenceStarted = false;
                _victorySequenceStarted = false;
                _victoryStartTicks = 0;
                ClearVictoryRewardState();
                GameState.WorldFade.RequestFadeIn(1.5f, "SceneReset");
                if (logPhaseTiming)
                {
                    LogUpdatePhaseTiming(
                        (int)FrameCounter,
                        activeWorld.Count,
                        deepCopiedWorld.Count,
                        renderedList.Count,
                        projectedCoordinates?.Count ?? 0,
                        particleObjectList.Count,
                        weaponObjectList.Count,
                        shadowObjectList.Count,
                        copyMs,
                        starfieldMs,
                        prepMs,
                        moveRotateMs,
                        mergeMs,
                        offscreenAiMs,
                        infectionMs,
                        projectMs,
                        crashMs,
                        cleanupMs,
                        directorHudMs,
                        musicMs,
                        resetFrame: true);
                }
                TrackFrameTiming((int)FrameCounter);
                return [];
            }

            if (doAiMark)
            {
                AiUpdateCounter = 0;
                foreach (var aiObject in GameState.SurfaceState.AiObjects)
                {
                    if (!aiObject.IsActive) continue;
                    if (aiObject.IsOnScreen == false)
                    {
                        aiObject.Movement.MoveObject(aiObject, audioPlayer, soundRegistry);
                        PublishObjectExplodedIfNeeded(world, aiObject);
                        aiObject.IsOnScreen = false;
                    }
                }
            }
            offscreenAiMs = MarkPhase();

            // Process cascading local infection spread (seeder-infected tiles spread to neighbors after a delay)
            SeederControls.ProcessLocalInfectionSpread(GameState.SurfaceState);
            HandleBiomassWarnings();
            infectionMs = MarkPhase();

            projectedCoordinates = From3dTo2d.ConvertTo2dFromObjects(renderedList, FrameCounter, projectedCoordinates);
            projectMs = MarkPhase();

            CrashDetection.HandleCrashboxes(renderedList, world.IsPaused);
            crashMs = MarkPhase();

            CleanupExplodedObjects(world);
            cleanupMs = MarkPhase();

            // Scene director: centralizes drone activation, mothership activation,
            // victory/defeat conditions, and checkpoint logic per scene.
            var director = activeScene?.Director;
            if (!_deathSequenceStarted && !_victorySequenceStarted && director != null)
            {
                director.Update();
            }

            UpdateHudState(world);

            // Victory: the director signals when all scene objectives are met
            if (!_deathSequenceStarted && !_victorySequenceStarted && director != null && director.IsVictory)
            {
                _victorySequenceStarted = true;
                _victoryStartTicks = Stopwatch.GetTimestamp();
                BeginVictoryRewardOverlay(activeScene?.SceneType ?? SceneTypes.Game);
            }

            // Victory timer: show overlay briefly then trigger scene transition
            if (_victorySequenceStarted && !_deathSequenceStarted && !GameState.WorldFade.IsFadeOutPendingOrActive)
            {
                float elapsed = (Stopwatch.GetTimestamp() - _victoryStartTicks) / (float)Stopwatch.Frequency;
                if (_victoryRewardBreakdown != null)
                {
                    float progress = Math.Clamp(elapsed / VictoryRewardCountUpSeconds, 0f, 1f);
                    UpdateVictoryRewardOverlay(progress);

                    if (elapsed >= VictoryRewardCountUpSeconds + VictoryRewardHoldSeconds)
                    {
                        GameState.WorldFade.RequestFadeOut(1.0f, WorldFadeState.VictoryCompleteReason);
                    }
                }
                else if (elapsed >= VictoryDisplaySeconds)
                {
                    GameState.WorldFade.RequestFadeOut(1.0f, WorldFadeState.VictoryCompleteReason);
                }
            }
            directorHudMs = MarkPhase();

            if (activeScene != null)
            {
                HandleMusic(renderedList, activeScene.SceneMusic);
                ShipAiVoiceService.Shared.TrySpeakPendingGameplayCue(
                    activeScene.SceneType == SceneTypes.Game || activeScene.SceneType == SceneTypes.Simulation,
                    audioPlayer,
                    soundRegistry);
            }
            musicMs = MarkPhase();

            if (logPhaseTiming)
            {
                LogUpdatePhaseTiming(
                    (int)FrameCounter,
                    activeWorld.Count,
                    deepCopiedWorld.Count,
                    renderedList.Count,
                    projectedCoordinates.Count,
                    particleObjectList.Count,
                    weaponObjectList.Count,
                    shadowObjectList.Count,
                    copyMs,
                    starfieldMs,
                    prepMs,
                    moveRotateMs,
                    mergeMs,
                    offscreenAiMs,
                    infectionMs,
                    projectMs,
                    crashMs,
                    cleanupMs,
                    directorHudMs,
                    musicMs,
                    resetFrame: false);
            }

            TrackFrameTiming((int)FrameCounter);
            return projectedCoordinates;
        }

        private void EnsureExplosionCleanupSubscription(IGameEventBus? eventBus)
        {
            if (ReferenceEquals(explosionCleanupEventBus, eventBus))
            {
                return;
            }

            if (explosionCleanupEventBus != null)
            {
                explosionCleanupEventBus.Unsubscribe(GameEventType.ObjectExploded, QueueExplosionCleanup);
            }

            explosionCleanupEventBus = eventBus;
            pendingExplosionCleanupIds.Clear();
            publishedExplosionIds.Clear();

            if (explosionCleanupEventBus != null)
            {
                explosionCleanupEventBus.Subscribe(GameEventType.ObjectExploded, QueueExplosionCleanup);
            }
        }

        private void QueueExplosionCleanup(IGameEvent gameEvent)
        {
            if (gameEvent.Source is not _3dObject obj || obj.ObjectName == "Ship")
            {
                return;
            }

            pendingExplosionCleanupIds.Add(obj.ObjectId);
        }

        private void PublishObjectExplodedIfNeeded(I3dWorld world, _3dObject obj)
        {
            if (obj.ObjectName == "Ship" || obj.ImpactStatus?.HasExploded != true)
            {
                return;
            }

            if (!publishedExplosionIds.Add(obj.ObjectId))
            {
                return;
            }

            world.EventBus?.Publish(new GameEvent
            {
                Type = GameEventType.ObjectExploded,
                Source = obj,
                ObjectName = obj.ObjectName,
                HasPowerUp = obj.HasPowerUp
            });
        }

        private void CleanupExplodedObjects(I3dWorld world)
        {
            bool useEventDrivenCleanup = explosionCleanupEventBus != null;
            if (useEventDrivenCleanup && pendingExplosionCleanupIds.Count == 0)
            {
                return;
            }

            lock (_lock)
            {
                List<_3dObject>? explodedObjects = null;
                HashSet<int>? explodedIds = null;
                HashSet<int>? observedPendingIds = useEventDrivenCleanup ? new HashSet<int>() : null;

                var inhabitants = world.WorldInhabitants;
                for (int i = 0; i < inhabitants.Count; i++)
                {
                    if (inhabitants[i] is not _3dObject obj)
                    {
                        continue;
                    }

                    if (useEventDrivenCleanup)
                    {
                        if (!pendingExplosionCleanupIds.Contains(obj.ObjectId))
                        {
                            continue;
                        }

                        observedPendingIds?.Add(obj.ObjectId);
                    }

                    if (obj.ObjectName == "Ship" || obj.ImpactStatus?.HasExploded != true)
                    {
                        continue;
                    }

                    explodedObjects ??= new List<_3dObject>();
                    explodedIds ??= new HashSet<int>(inhabitants.Count);
                    explodedObjects.Add(obj);
                    explodedIds.Add(obj.ObjectId);
                }

                if (useEventDrivenCleanup)
                {
                    ClearMissingExplosionCleanupIds(observedPendingIds);
                }

                if (explodedObjects == null || explodedIds == null)
                {
                    return;
                }

                // Spawn PowerUp objects at the location of exploded objects that have the flag
                // and award score for enemy kills / trigger checkpoints
                var gps = GameState.GamePlayState;
                bool isTutorialScene = gps.CurrentSceneType == SceneTypes.Tutorial;
                bool checkpointTriggered = false;
                bool powerUpAlreadyExists = false;
                for (int i = 0; i < inhabitants.Count; i++)
                {
                    if (inhabitants[i].ObjectName == "PowerUp")
                    {
                        powerUpAlreadyExists = true;
                        break;
                    }
                }

                var aiObjects = GameState.SurfaceState.AiObjects;

                foreach (var obj in explodedObjects)
                {
                    if (EnemySetup.IsEnemyTypeValid(obj.ObjectName))
                    {
                        if (!isTutorialScene)
                        {
                            gps.RecordKill(obj.ObjectName);

                            world.EventBus?.Publish(new GameEvent
                            {
                                Type = GameEventType.EnemyDestroyed,
                                Source = obj,
                                ObjectName = obj.ObjectName,
                                HasPowerUp = obj.HasPowerUp,
                                PowerUpType = obj.PowerUpType,
                                SceneType = gps.CurrentSceneType,
                                SceneIndex = gps.SceneIndex,
                                Score = gps.Score,
                                TotalKills = gps.TotalKills,
                                TotalShotsFired = gps.TotalShotsFired,
                                TotalDeaths = gps.TotalDeaths,
                                Accuracy = gps.Accuracy,
                                PowerUpsCollected = gps.PowerUpsCollected,
                                SpeedPowerUpLevel = gps.SpeedPowerUpLevel
                            });
                        }

                        if (Logger.ShouldLog(enableProgressionLogging))
                        {
                            var pos = obj.WorldPosition;
                            var offs = obj.ObjectOffsets;
                            Logger.Log(
                                $"EnemyKilled: name={obj.ObjectName}; id={obj.ObjectId}; world=({pos?.x ?? 0f};{pos?.y ?? 0f};{pos?.z ?? 0f}); offsets=({offs?.x ?? 0f};{offs?.y ?? 0f};{offs?.z ?? 0f}); status={GetEnemyStatusSnapshot(aiObjects, explodedIds)}",
                                "Progression");
                        }

                        // The scene policy assigns both drop timing and type at kill time.
                        // Seeders themselves remain location-independent and unflagged at spawn.
                        if (!isTutorialScene && obj.ObjectName == "Seeder")
                        {
                            if (PowerUpDropPolicy.TryConsumeDrop(out var powerUpType, canAward: !obj.HasPowerUp))
                            {
                                obj.HasPowerUp = true;
                                obj.PowerUpType = powerUpType;
                            }
                        }

                        if (!isTutorialScene && GameSetup.IsCheckpointEnemy(obj.ObjectName, obj.HasPowerUp))
                            checkpointTriggered = true;
                    }

                    if (obj.HasPowerUp && obj.WorldPosition != null)
                    {
                        // Standard drops stay limited to one at a time. A scene-authored
                        // speed pickup must not disappear while a standard drop is waiting.
                        if (powerUpAlreadyExists && obj.PowerUpType == PowerUpType.Standard)
                            continue;

                        var powerup = CreatePowerUpDrop(obj);
                        inhabitants.Add(powerup);
                        GameState.SurfaceState.AiObjects.Add(powerup);
                        powerUpAlreadyExists = true;
                    }
                }

                for (int i = inhabitants.Count - 1; i >= 0; i--)
                {
                    if (explodedIds.Contains(inhabitants[i].ObjectId))
                    {
                        inhabitants.RemoveAt(i);
                    }
                }

                for (int i = aiObjects.Count - 1; i >= 0; i--)
                {
                    if (explodedIds.Contains(aiObjects[i].ObjectId))
                    {
                        aiObjects.RemoveAt(i);
                    }
                }

                if (checkpointTriggered)
                {
                    // Recount remaining enemies after removal so the checkpoint
                    // reflects the actual state, not the stale previous-frame values.
                    int seedersLeft = 0, dronesLeft = 0, motherShipsLeft = 0;
                    for (int i = 0; i < aiObjects.Count; i++)
                    {
                        var o = aiObjects[i];
                        if (o.ImpactStatus?.HasExploded == true) continue;
                        if (o.ObjectName == "Seeder") seedersLeft++;
                        else if (o.ObjectName == "KamikazeDrone" && o.IsActive) dronesLeft++;
                        else if ((o.ObjectName == "MotherShipSmall" || o.ObjectName == "MotherShipMedium" || o.ObjectName == "MotherShipLarge") && o.IsActive) motherShipsLeft++;
                    }
                    gps.SeedersRemaining = seedersLeft;
                    gps.DronesRemaining = dronesLeft;
                    gps.MotherShipsRemaining = motherShipsLeft;

                    gps.SaveCheckpoint();
                    try
                    {
                        GameStatePersistence.SaveGameState();
                        ShipAiVoiceService.Shared.RequestGameplaySaveConfirmation();
                    }
                    catch { }
                    try { HighscoreService.SubmitFromGamePlay(gps); } catch { }
                }

                var bestCandidateStates = GameState.ShipState.BestCandidateStates;
                for (int i = bestCandidateStates.Count - 1; i >= 0; i--)
                {
                    var enemyObject = bestCandidateStates[i]?.BestEnemyCandidate?.EnemyObject;
                    if (enemyObject != null && explodedIds.Contains(enemyObject.ObjectId))
                    {
                        bestCandidateStates.RemoveAt(i);
                    }
                }

                CleanupWorldObjects(explodedObjects);
                ClearExplosionCleanupIds(explodedIds);
            }
        }

        private static _3dObject CreatePowerUpDrop(_3dObject source)
        {
            var powerup = PowerUp.CreatePowerup(source.ParentSurface, source.PowerUpType);
            var sourceWorld = source.WorldPosition ?? new Vector3();
            var sourceOffsets = source.ObjectOffsets ?? new Vector3();

            // PowerUpControls will apply surface Y sync on its first frame.
            // Store raw Y here, but preserve X/Z so the drop keeps the source's render position.
            var globalMapY = GameState.SurfaceState?.GlobalMapPosition?.y ?? 0f;
            var rawSourceY = sourceOffsets.y - globalMapY * SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY;

            powerup.WorldPosition = new Vector3
            {
                x = sourceWorld.x,
                y = sourceWorld.y,
                z = sourceWorld.z
            };
            powerup.ObjectOffsets = new Vector3
            {
                x = sourceOffsets.x,
                y = rawSourceY - 50f,
                z = sourceOffsets.z
            };
            powerup.Movement = new PowerUpControls();
            return powerup;
        }

        private void ClearMissingExplosionCleanupIds(HashSet<int>? observedPendingIds)
        {
            if (observedPendingIds == null || observedPendingIds.Count == pendingExplosionCleanupIds.Count)
            {
                return;
            }

            foreach (var objectId in pendingExplosionCleanupIds.ToList())
            {
                if (observedPendingIds.Contains(objectId))
                {
                    continue;
                }

                pendingExplosionCleanupIds.Remove(objectId);
                publishedExplosionIds.Remove(objectId);
            }
        }

        private void ClearExplosionCleanupIds(HashSet<int> cleanupIds)
        {
            foreach (var objectId in cleanupIds)
            {
                pendingExplosionCleanupIds.Remove(objectId);
                publishedExplosionIds.Remove(objectId);
            }
        }

        private static void CleanupWorldObjects(List<_3dObject> objects)
        {
            foreach (var obj in objects)
            {
                TryDisposeMovement(obj);

                if (obj.Particles != null)
                {
                    obj.Particles.Particles.Clear();
                    obj.Particles = null;
                }

                if (obj.WeaponSystems != null)
                {
                    obj.WeaponSystems.ActiveWeapons.Clear();
                    obj.WeaponSystems = null;
                }

                obj.CrashBoxes?.Clear();
                obj.ObjectParts?.Clear();
                obj.CalculatedCrashOffset = null;
                obj.WorldPosition = null;
                obj.ObjectOffsets = null;
                obj.ParentSurface = null;
                obj.Movement = null;
                obj.ImpactStatus = null;
            }
        }

        public void StopNonMusicAudio()
        {
            ShipAiVoiceService.Shared.StopCurrentSpeech();
            audioPlayer.StopNonMusic();
        }

        private static string GetEnemyStatusSnapshot(List<_3dObject> aiObjects, HashSet<int> pendingRemovalIds)
        {
            int liveSeeders = 0;
            int liveDrones = 0;
            int liveMotherShips = 0;
            int liveOtherEnemies = 0;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var aiObject = aiObjects[i];
                if (pendingRemovalIds.Contains(aiObject.ObjectId))
                    continue;
                if (aiObject.ImpactStatus?.HasExploded == true)
                    continue;
                if (!EnemySetup.IsEnemyTypeValid(aiObject.ObjectName))
                    continue;

                if (aiObject.ObjectName == "Seeder")
                    liveSeeders++;
                else if (aiObject.ObjectName == "KamikazeDrone" && aiObject.IsActive)
                    liveDrones++;
                else if ((aiObject.ObjectName == "MotherShipSmall" || aiObject.ObjectName == "MotherShipMedium" || aiObject.ObjectName == "MotherShipLarge") && aiObject.IsActive)
                    liveMotherShips++;
                else
                    liveOtherEnemies++;
            }

            var gps = GameState.GamePlayState;
            return $"liveSeeders={liveSeeders}; liveDrones={liveDrones}; liveMotherShips={liveMotherShips}; liveOtherEnemies={liveOtherEnemies}; gpsSeeders={gps.SeedersRemaining}; gpsDrones={gps.DronesRemaining}; gpsMotherShips={gps.MotherShipsRemaining}; initialSeeders={gps.InitialSeeders}; initialDrones={gps.InitialDrones}";
        }

        private static void TryDisposeMovement(_3dObject obj)
        {
            var movement = obj?.Movement;
            if (movement == null)
            {
                return;
            }

            var movementType = movement.GetType();
            lock (movementDisposeNotImplementedTypesLock)
            {
                if (movementDisposeNotImplementedTypes.Contains(movementType))
                {
                    return;
                }
            }

            try
            {
                movement.Dispose();
            }
            catch (NotImplementedException)
            {
                lock (movementDisposeNotImplementedTypesLock)
                {
                    movementDisposeNotImplementedTypes.Add(movementType);
                }
            }
        }

        /// <summary>
        /// Syncs per-frame game state into GamePlayState for the HUD:
        /// ship health from ImpactStatus, and surviving enemy counts.
        /// </summary>
        private static void UpdateHudState(I3dWorld world)
        {
            var gps = GameState.GamePlayState;

            // Sync ship health from the actual object into GamePlayState
            var inhabitants = world.WorldInhabitants;
            for (int i = 0; i < inhabitants.Count; i++)
            {
                if (inhabitants[i].ObjectName == "Ship")
                {
                    int hp = inhabitants[i].ImpactStatus?.ObjectHealth ?? 0;
                    gps.Health = hp;
                    break;
                }
            }

            // Count surviving enemies
            var aiObjects = GameState.SurfaceState.AiObjects;
            int drones = 0;
            int seeders = 0;
            int motherShips = 0;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i];
                if (obj.ImpactStatus?.HasExploded == true)
                    continue;

                if (obj.ObjectName == "KamikazeDrone" && obj.IsActive)
                    drones++;
                else if (obj.ObjectName == "Seeder")
                    seeders++;
                else if ((obj.ObjectName == "MotherShipSmall" || obj.ObjectName == "MotherShipMedium" || obj.ObjectName == "MotherShipLarge") && obj.IsActive)
                    motherShips++;
            }

            // Capture initial totals once (first frame where enemies exist)
            if (gps.InitialDrones == 0 && drones > 0)
                gps.InitialDrones = drones;
            if (gps.InitialSeeders == 0 && seeders > 0)
                gps.InitialSeeders = seeders;
            if (gps.InitialMotherShips == 0 && motherShips > 0)
                gps.InitialMotherShips = motherShips;

            gps.DronesRemaining = drones;
            gps.SeedersRemaining = seeders;
            gps.MotherShipsRemaining = motherShips;

            // MotherShip health bar tracking
            bool foundMotherShip = false;
            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i];
                if (!EnemySetup.IsMotherShipType(obj.ObjectName) || !obj.IsActive)
                    continue;

                if (obj.ImpactStatus?.HasExploded == true)
                    continue;

                foundMotherShip = true;
                float aggression = obj.ObjectName switch
                {
                    "MotherShipLarge" => gps.MotherShipLargeAggression,
                    "MotherShipMedium" => gps.MotherShipMediumAggression,
                    _ => gps.MotherShipSmallAggression
                };
                int maxHealth = EnemySetup.GetMotherShipHealth(obj.ObjectName, aggression);
                int currentHealth = obj.ImpactStatus?.ObjectHealth ?? maxHealth;
                float healthPct = (float)currentHealth / maxHealth;

                gps.ShowMotherShipHealthBar = true;

                gps.MotherShipHealthPercent = healthPct;

                var localWorldPos = obj.GetLocalWorldPosition();
                if (localWorldPos != null)
                {
                    float sx = ScreenSetup.screenSizeX / 2f - localWorldPos.x + (obj.ObjectOffsets?.x ?? 0);
                    float sy = ScreenSetup.screenSizeY / 2f - localWorldPos.y + (obj.ObjectOffsets?.y ?? 0);
                    gps.MotherShipScreenX = sx;
                    gps.MotherShipScreenY = sy;
                    gps.MotherShipIsOnScreen = sx > -100 && sx < ScreenSetup.screenSizeX + 100
                                             && sy > -100 && sy < ScreenSetup.screenSizeY + 100;
                }
                else
                {
                    gps.MotherShipIsOnScreen = false;
                }
                break;
            }

            if (!foundMotherShip)
            {
                gps.ShowMotherShipHealthBar = false;
                gps.MotherShipIsOnScreen = false;
            }
        }

        private void BeginVictoryRewardOverlay(SceneTypes sceneType)
        {
            var overlay = GameState.ScreenOverlayState;
            overlay.ResetToDefaults();
            overlay.Type = ScreenOverlayType.Game;
            overlay.Anchor = ScreenOverlayAnchor.Center;
            overlay.Header = "PLANET SECURED";
            overlay.Title = "MISSION REWARD";
            overlay.Footer = "CALCULATING PLANET BONUS";
            overlay.DimStrength = 0.55f;
            overlay.PanelWidthRatio = 0.66f;
            overlay.PanelHeightRatio = 0.38f;
            overlay.PanelYOffsetRatio = 0.02f;
            overlay.CenterText = false;
            overlay.ShowOverlay = true;
            overlay.AutoHide = false;
            overlay.ShowDebugOverlay = false;

            if (sceneType != SceneTypes.Game && sceneType != SceneTypes.Simulation)
            {
                _victoryRewardBreakdown = null;
                _lastAppliedVictoryReward = 0;
                overlay.Title = "ALL THREATS ELIMINATED";
                overlay.Body = "Proceeding to next sector...";
                overlay.Footer = "";
                overlay.PanelHeightRatio = 0.22f;
                return;
            }

            _victoryRewardBreakdown = PlanetRewardCalculator.Calculate(GameState.GamePlayState);
            _lastAppliedVictoryReward = 0;
            UpdateVictoryRewardOverlay(0f);
        }

        private void UpdateVictoryRewardOverlay(float progress)
        {
            if (_victoryRewardBreakdown == null)
                return;

            progress = Math.Clamp(progress, 0f, 1f);
            int displayedReward = _victoryRewardBreakdown.GetDisplayedTotal(progress);
            int delta = displayedReward - _lastAppliedVictoryReward;
            if (delta > 0)
            {
                GameState.GamePlayState.Score += delta;
                _lastAppliedVictoryReward = displayedReward;
            }

            var overlay = GameState.ScreenOverlayState;
            overlay.Body = _victoryRewardBreakdown.BuildOverlayBody(progress);
            overlay.Footer = progress >= 1f
                ? $"BONUS APPLIED  //  SCORE {GameState.GamePlayState.Score:N0}"
                : $"COUNTING BONUS  //  SCORE {GameState.GamePlayState.Score:N0}";
        }

        private void ClearVictoryRewardState()
        {
            _victoryRewardBreakdown = null;
            _lastAppliedVictoryReward = 0;
        }

        private Dictionary<int, _3dObject> InitializeAiOnScreenTracking()
        {
            var aiObjects = GameState.SurfaceState.AiObjects;
            if (aiObjects == null || aiObjects.Count == 0)
                return null;

            var aiById = aiByIdBuffer;
            aiById.Clear();
            aiById.EnsureCapacity(aiObjects.Count);

            foreach (var ai in aiObjects)
            {
                ai.IsOnScreen = false;
                aiById[ai.ObjectId] = ai;
            }

            return aiById;
        }

        private static void EnsureListCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
                list.Capacity = capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetAiIsOnScreen(
            Dictionary<int, _3dObject> aiById,
            int objectId
        )
        {
            if (aiById != null && aiById.TryGetValue(objectId, out var aiObj))
            {
                aiObj.IsOnScreen = true;
            }
        }

        public void HandleMusic(List<_3dObject> renderedObjects, string sceneMusic)
        {
            if (string.IsNullOrWhiteSpace(sceneMusic))
            {
                return;
            }

            bool sceneMusicChanged = !string.Equals(CurrentSceneMusicId, sceneMusic, StringComparison.Ordinal);

            if (sceneMusicChanged)
            {
                audioPlayer.StopMusic();
                MusicDef = soundRegistry.Get(sceneMusic);
                CurrentSceneMusicId = sceneMusic;
                MusicIsPlaying = false;
            }

            if (!MusicIsPlaying && MusicDef != null)
            {
                audioPlayer.PlayMusic(MusicDef, GameState.SettingsState.ApplyMusicVolume(DefaultMusicVolume));
                MusicIsPlaying = true;
                AppliedAudioSettingsVersion = GameState.SettingsState.Version;
            }
            else if (MusicIsPlaying && AppliedAudioSettingsVersion != GameState.SettingsState.Version)
            {
                audioPlayer.SetMusicVolume(GameState.SettingsState.ApplyMusicVolume(DefaultMusicVolume));
                AppliedAudioSettingsVersion = GameState.SettingsState.Version;
            }
        }

        private static float GetWeatherEffectsOpacity(float baseOpacity)
        {
            var settings = GameState.SettingsState;
            if (settings == null || !settings.EnhancedWeatherEnabled)
                return 0f;

            return Math.Clamp(baseOpacity * settings.ParticleDensityMultiplier, 0f, 1f);
        }

        private void HandleBiomassWarnings()
        {
            var gameplay = GameState.GamePlayState;

            if (gameplay.InfectionCriticalProgress <= 0.01f)
            {
                _biomassCriticalWarningPlayed = false;
                _biomassAbortWarningPlayed = false;
                return;
            }

            if (_deathSequenceStarted || _victorySequenceStarted)
                return;

            if (!_biomassAbortWarningPlayed && gameplay.IsBiomassAbortWarning)
            {
                _biomassAbortWarningPlayed = true;
                _biomassCriticalWarningPlayed = true;

                if (soundRegistry.TryGet(BiomassAbortWarningSoundId, out var abortDefinition))
                {
                    audioPlayer.PlayOneShot(abortDefinition);
                }

                return;
            }

            if (!_biomassCriticalWarningPlayed && gameplay.IsBiomassCriticalWarning)
            {
                _biomassCriticalWarningPlayed = true;

                if (soundRegistry.TryGet(BiomassCriticalWarningSoundId, out var criticalDefinition))
                {
                    audioPlayer.PlayOneShot(criticalDefinition);
                }
            }
        }

        private static void ShowPlanetLostChoiceOverlay(I3dWorld world)
        {
            var overlay = GameState.ScreenOverlayState;
            if (overlay.ShowOverlay && overlay.ChoiceAction == ScreenOverlayChoiceAction.PlanetLostRecovery)
                return;

            overlay.ResetToDefaults();
            overlay.Type = ScreenOverlayType.Game;
            overlay.Anchor = ScreenOverlayAnchor.Center;
            overlay.IsModal = true;
            overlay.Header = "BIOMASS CRITICAL";
            overlay.Title = "PLANET LOST";
            overlay.SetChoiceOptions(
                ScreenOverlayChoiceAction.PlanetLostRecovery,
                "The infection has overrun the planet.\nChoose how to continue:",
                "CONTINUE FROM CHECKPOINT",
                "RESET PLANET TO ARRIVAL");
            overlay.Footer = "UP/DOWN TO SELECT  //  ENTER TO CONFIRM";
            overlay.DimStrength = 0.65f;
            overlay.PanelWidthRatio = 0.68f;
            overlay.PanelHeightRatio = 0.32f;
            overlay.PanelYOffsetRatio = 0.00f;
            overlay.CenterText = false;
            overlay.ShowOverlay = true;
            overlay.AutoHide = false;
            overlay.ShowDebugOverlay = false;

            world.IsPaused = true;
            GameState.GamePlayState.Phase = GamePhase.Paused;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<List<IVector3>> RotateAllCrashboxes(List<List<IVector3>> crashboxes, Vector3 rotation)
        {
            var rotatedCrashboxes = new List<List<IVector3>>(crashboxes.Count);
            foreach (var crashbox in crashboxes)
            {
                var rotated = new List<IVector3>(crashbox.Count);
                foreach (var point in crashbox)
                {
                    rotated.Add(RotatePoint((Vector3)point, rotation));
                }
                rotatedCrashboxes.Add(rotated);
            }
            return rotatedCrashboxes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IVector3 RotatePoint(Vector3 point, Vector3 rotation)
        {
            var rotatedPoint = Rotate3d.RotatePoint(rotation.z, point, 'Z');
            rotatedPoint = Rotate3d.RotatePoint(rotation.y, rotatedPoint, 'Y');
            rotatedPoint = Rotate3d.RotatePoint(rotation.x, rotatedPoint, 'X');
            return rotatedPoint;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private List<ITriangleMeshWithColor> RotateMesh(List<ITriangleMeshWithColor> mesh, Vector3 rotation)
        {
            var rotatedMesh = Rotate3d.RotateMesh(mesh, rotation.z, 'Z');
            rotatedMesh = Rotate3d.RotateMesh(rotatedMesh, rotation.y, 'Y');
            rotatedMesh = Rotate3d.RotateMesh(rotatedMesh, rotation.x, 'X');
            return rotatedMesh;
        }

        private void SetMovementGuides(_3dObject inhabitant, I3dObjectPart part, List<ITriangleMeshWithColor> rotatedMesh)
        {
            switch (part.PartName)
            {
                case "SeederParticlesStartGuide":
                    inhabitant.Movement.SetParticleGuideCoordinates(rotatedMesh.First() as TriangleMeshWithColor, null);
                    break;
                case "SeederParticlesGuide":
                    inhabitant.Movement.SetParticleGuideCoordinates(null, rotatedMesh.First() as TriangleMeshWithColor);
                    break;
                case "JetMotor":
                    inhabitant.Movement.SetParticleGuideCoordinates(rotatedMesh.First() as TriangleMeshWithColor, null);
                    break;
                case "WeaponDirectionGuide":
                    inhabitant.Movement.SetWeaponGuideCoordinates(null, rotatedMesh.First() as TriangleMeshWithColor);
                    break;
                case "WeaponStartGuide":
                    inhabitant.Movement.SetWeaponGuideCoordinates(rotatedMesh.First() as TriangleMeshWithColor, null);
                    break;
                case "LaserDirectionGuide":
                    inhabitant.Movement.SetWeaponGuideCoordinates(null, rotatedMesh.First() as TriangleMeshWithColor);
                    break;
                case "LaserStartGuide":
                    inhabitant.Movement.SetWeaponGuideCoordinates(rotatedMesh.First() as TriangleMeshWithColor, null);
                    break;
                case "JetMotorDirectionGuide":
                    if (Logger.ShouldLog(enableLocalLogging)) Logger.Log($"MainLoop Set Guide after rotation: {rotatedMesh.First().vert1.x + ", " + rotatedMesh.First().vert1.y + ", " + rotatedMesh.First().vert1.z} Inhabitant:{inhabitant.ObjectName} ");
                    inhabitant.Movement.SetParticleGuideCoordinates(null, rotatedMesh.First() as TriangleMeshWithColor);
                    break;
                case "RearEngine":
                    inhabitant.Movement.SetRearEngineGuideCoordinates(rotatedMesh.First() as TriangleMeshWithColor, null);
                    break;
                case "RearEngineDirectionGuide":
                    inhabitant.Movement.SetRearEngineGuideCoordinates(null, rotatedMesh.First() as TriangleMeshWithColor);
                    break;
                case "LeftWingEngineStart":
                    inhabitant.Movement.SetParticleGuideCoordinates(rotatedMesh.First() as TriangleMeshWithColor, null);
                    break;
                case "LeftWingEngineGuide":
                    inhabitant.Movement.SetParticleGuideCoordinates(null, rotatedMesh.First() as TriangleMeshWithColor);
                    break;
                case "RightWingEngineStart":
                    inhabitant.Movement.SetRearEngineGuideCoordinates(rotatedMesh.First() as TriangleMeshWithColor, null);
                    break;
                case "RightWingEngineGuide":
                    inhabitant.Movement.SetRearEngineGuideCoordinates(null, rotatedMesh.First() as TriangleMeshWithColor);
                    break;
                case "BomberBombDropStart":
                    inhabitant.Movement.SetWeaponGuideCoordinates(rotatedMesh.First() as TriangleMeshWithColor, null);
                    break;
                case "BomberBombDropEnd":
                    inhabitant.Movement.SetWeaponGuideCoordinates(null, rotatedMesh.First() as TriangleMeshWithColor);
                    break;
            }
        }

        private void TrackFrameTiming(int frameIndex)
        {
            if (!frameTimer.IsRunning)
                return;

            bool logFrameTiming = Logger.ShouldLog(EnableCpuHeadroomLogging);
            if (!logFrameTiming && !EnableAdaptiveGc)
            {
                frameTimer.Stop();
                return;
            }

            var budgetMs = CommonUtilities.CommonSetup.ScreenSetup.TargetFrameIntervalMs;
            var preGcElapsedMs = frameTimer.Elapsed.TotalMilliseconds;
            var preGcHeadroomMs = budgetMs - preGcElapsedMs;
            var preGcHeadroomPct = (preGcHeadroomMs / budgetMs) * 100.0;
            var adaptiveGc = TryRunAdaptiveGc(frameIndex, preGcHeadroomMs, preGcHeadroomPct);

            frameTimer.Stop();
            var elapsedMs = frameTimer.Elapsed.TotalMilliseconds;
            var headroomMs = budgetMs - elapsedMs;
            var headroomPct = (headroomMs / budgetMs) * 100.0;

            if (!logFrameTiming)
                return;

            performanceFrameCount++;
            averageFrameMs += (elapsedMs - averageFrameMs) / performanceFrameCount;
            averageHeadroomMs += (headroomMs - averageHeadroomMs) / performanceFrameCount;

            DebugMessage += $" PerfHeadroom: {headroomPct:0.#}%";

            if (adaptiveGc.HasValue)
            {
                var gc = adaptiveGc.Value;
                Logger.Log(
                    $"[IdleGc] frame={frameIndex} gen={gc.Generation} gcMs={gc.ElapsedMs:0.###} allocatedMb={gc.AllocatedSinceLastMb:0.###} " +
                    $"preHeadroomPct={preGcHeadroomPct:0.#} postHeadroomPct={headroomPct:0.#} gen0Collections={gc.Gen0Collections} gen1Collections={gc.Gen1Collections}");
            }

            if (performanceFrameCount % PerfLogInterval == 0)
            {
                var avgHeadroomPct = (averageHeadroomMs / budgetMs) * 100.0;
                Logger.Log($"[LivePerf] frame={frameIndex} frameMs={elapsedMs:0.###} headroomMs={headroomMs:0.###} headroomPct={headroomPct:0.#} avgFrameMs={averageFrameMs:0.###} avgHeadroomMs={averageHeadroomMs:0.###} avgHeadroomPct={avgHeadroomPct:0.#}");
            }
        }

        private AdaptiveGcResult? TryRunAdaptiveGc(int frameIndex, double headroomMs, double headroomPct)
        {
            if (!EnableAdaptiveGc)
                return null;

            if (headroomMs < adaptiveGcMinHeadroomMs || headroomPct < adaptiveGcMinHeadroomPct)
                return null;

            if (frameIndex - lastAdaptiveGcFrame < AdaptiveGcMinFrameInterval)
                return null;

            long allocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
            long allocatedSinceLast = allocatedBytes - lastAdaptiveGcAllocatedBytes;
            if (allocatedSinceLast < adaptiveGcMinAllocatedBytes)
                return null;

            int generation = ((adaptiveGcAttempts + 1) % adaptiveGcGen1EveryAttempts == 0)
                ? 1
                : 0;

            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            long startTicks = Stopwatch.GetTimestamp();

            GC.Collect(generation, GCCollectionMode.Optimized, blocking: false, compacting: false);

            double elapsedMs = TicksToMs(Stopwatch.GetTimestamp() - startTicks);
            int gen0Collections = GC.CollectionCount(0) - gen0Before;
            int gen1Collections = GC.CollectionCount(1) - gen1Before;

            lastAdaptiveGcFrame = frameIndex;
            lastAdaptiveGcAllocatedBytes = allocatedBytes;
            adaptiveGcAttempts++;

            return new AdaptiveGcResult(
                generation,
                elapsedMs,
                allocatedSinceLast / (1024.0 * 1024.0),
                gen0Collections,
                gen1Collections);
        }

        private readonly struct AdaptiveGcResult
        {
            public AdaptiveGcResult(
                int generation,
                double elapsedMs,
                double allocatedSinceLastMb,
                int gen0Collections,
                int gen1Collections)
            {
                Generation = generation;
                ElapsedMs = elapsedMs;
                AllocatedSinceLastMb = allocatedSinceLastMb;
                Gen0Collections = gen0Collections;
                Gen1Collections = gen1Collections;
            }

            public int Generation { get; }
            public double ElapsedMs { get; }
            public double AllocatedSinceLastMb { get; }
            public int Gen0Collections { get; }
            public int Gen1Collections { get; }
        }

        private static void LogUpdatePhaseTiming(
            int frameIndex,
            int activeCount,
            int copiedCount,
            int renderedCount,
            int projectedCount,
            int particleCount,
            int weaponCount,
            int shadowCount,
            double copyMs,
            double starfieldMs,
            double prepMs,
            double moveRotateMs,
            double mergeMs,
            double offscreenAiMs,
            double infectionMs,
            double projectMs,
            double crashMs,
            double cleanupMs,
            double directorHudMs,
            double musicMs,
            bool resetFrame)
        {
            Logger.Log(
                $"[UpdatePhasePerf] frame={frameIndex} reset={resetFrame} active={activeCount} copied={copiedCount} rendered={renderedCount} projected={projectedCount} particles={particleCount} weapons={weaponCount} shadows={shadowCount} " +
                $"copyMs={copyMs:0.###} starfieldMs={starfieldMs:0.###} prepMs={prepMs:0.###} moveRotateMs={moveRotateMs:0.###} mergeMs={mergeMs:0.###} offscreenAiMs={offscreenAiMs:0.###} " +
                $"infectionMs={infectionMs:0.###} projectMs={projectMs:0.###} crashMs={crashMs:0.###} cleanupMs={cleanupMs:0.###} directorHudMs={directorHudMs:0.###} musicMs={musicMs:0.###}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double TicksToMs(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

    }
}
