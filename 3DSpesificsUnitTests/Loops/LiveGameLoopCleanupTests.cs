using _3dTesting.MainWindowClasses.Loops;
using _3dTesting._Coordinates;
using CommonUtilities._3DHelpers;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.Events;
using CommonUtilities.GamePlayHelpers;
using CommonUtilities.Persistence;
using Domain;
using System.Diagnostics;
using System.Reflection;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Loops;

[TestClass]
public class LiveGameLoopCleanupTests
{
    private string _originalLocalFolder = string.Empty;
    private string _testLocalFolder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainCleanupTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.Initialize();

        GameState.SurfaceState = new SurfaceState();
        GameState.GamePlayState = new GamePlayState();
        GameState.ShipState = new ShipState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.WorldFade = new WorldFadeState();
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

    [TestMethod]
    public void CleanupExplodedObjects_RemovesOnlyObjectsQueuedByExplosionEvent()
    {
        var bus = new GameEventBus();
        var world = new TestWorld
        {
            EventBus = bus,
            WorldInhabitants = new List<I3dObject>
            {
                CreateExplodedObject(10),
                CreateExplodedObject(11)
            }
        };

        var loop = new LiveGameLoop();
        InvokePrivate(loop, "EnsureExplosionCleanupSubscription", bus);

        bus.Publish(new GameEvent
        {
            Type = GameEventType.ObjectExploded,
            Source = world.WorldInhabitants[0],
            ObjectName = world.WorldInhabitants[0].ObjectName
        });

        InvokePrivate(loop, "CleanupExplodedObjects", world);

        Assert.IsFalse(world.WorldInhabitants.Any(x => x.ObjectId == 10));
        Assert.IsTrue(world.WorldInhabitants.Any(x => x.ObjectId == 11));
    }

    [TestMethod]
    public void CleanupExplodedObjects_PowerUpDropPreservesSourceLocalRenderOffsets()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = 500f, y = 80f, z = 900f };
        GameState.SurfaceState.AiObjects = new List<_3dObject>();
        var explodedSeeder = CreateExplodedObject(20);
        explodedSeeder.ObjectName = "Seeder";
        explodedSeeder.HasPowerUp = true;
        explodedSeeder.WorldPosition = new Vector3 { x = 1200f, y = 3f, z = 1800f };
        explodedSeeder.ObjectOffsets = new Vector3 { x = 35f, y = 260f, z = 125f };
        var sourceWorld = Copy(explodedSeeder.WorldPosition);
        var sourceOffsets = Copy(explodedSeeder.ObjectOffsets);
        GameState.SurfaceState.AiObjects.Add(explodedSeeder);
        var world = new TestWorld
        {
            WorldInhabitants = new List<I3dObject> { explodedSeeder }
        };

        var loop = new LiveGameLoop();

        InvokePrivate(loop, "CleanupExplodedObjects", world);

        var powerup = world.WorldInhabitants.OfType<_3dObject>().Single(x => x.ObjectName == "PowerUp");
        Assert.AreEqual(sourceWorld.x, powerup.WorldPosition!.x, 0.001f);
        Assert.AreEqual(sourceWorld.y, powerup.WorldPosition.y, 0.001f);
        Assert.AreEqual(sourceWorld.z, powerup.WorldPosition.z, 0.001f);
        Assert.AreEqual(sourceOffsets.x, powerup.ObjectOffsets!.x, 0.001f,
            "PowerUp drops must keep the source object's local X offset so they do not jump sideways.");
        Assert.AreEqual(sourceOffsets.z, powerup.ObjectOffsets.z, 0.001f,
            "PowerUp drops must keep the source object's local Z offset so they do not jump in depth.");
        Assert.AreEqual(sourceOffsets.y - GameState.SurfaceState.GlobalMapPosition.y * SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY - 50f,
            powerup.ObjectOffsets.y,
            0.001f,
            "PowerUp Y is stored as raw surface-sync input; PowerUpControls reapplies surface sync on the next frame.");
        Assert.IsTrue(GameState.SurfaceState.AiObjects.Any(x => x.ObjectId == powerup.ObjectId));
    }

    [TestMethod]
    public void CleanupExplodedObjects_KillTimePolicyPromotesSeederToPowerUpDrop()
    {
        // Configure a wave where the very first seeder kill should drop a powerup.
        _3dRotations.Helpers.PowerUpDropPolicy.ConfigureForWave(totalSeeders: 1, powerUpCount: 1);
        GameState.SurfaceState.GlobalMapPosition = new Vector3();
        GameState.SurfaceState.AiObjects = new List<_3dObject>();
        GameState.GamePlayState.CurrentSceneType = SceneTypes.Game;

        var explodedSeeder = CreateExplodedObject(30);
        explodedSeeder.ObjectName = "Seeder";
        explodedSeeder.HasPowerUp = false; // spawn-time flag stays false; promotion must happen here.
        explodedSeeder.WorldPosition = new Vector3 { x = 100f, y = 0f, z = 200f };
        explodedSeeder.ObjectOffsets = new Vector3 { x = 0f, y = -200f, z = 600f };
        GameState.SurfaceState.AiObjects.Add(explodedSeeder);

        var world = new TestWorld
        {
            WorldInhabitants = new List<I3dObject> { explodedSeeder }
        };

        var loop = new LiveGameLoop();

        InvokePrivate(loop, "CleanupExplodedObjects", world);

        bool spawnedPowerUp = world.WorldInhabitants.OfType<_3dObject>().Any(x => x.ObjectName == "PowerUp");
        Assert.IsTrue(spawnedPowerUp,
            "First seeder kill of a 1/1 wave must promote the seeder via PowerUpDropPolicy and drop a PowerUp.");
    }

    [TestMethod]
    public void CleanupExplodedObjects_SpeedDropSurvivesWhenStandardPowerUpAlreadyExists()
    {
        _3dRotations.Helpers.PowerUpDropPolicy.ConfigureForWave(
            totalSeeders: 21,
            powerUpCount: 2,
            firstKillPowerUpType: PowerUpType.TravelSpeedLevel1);
        GameState.SurfaceState.GlobalMapPosition = new Vector3();
        GameState.GamePlayState.CurrentSceneType = SceneTypes.Game;
        var existingPowerUp = CreateExplodedObject(32);
        existingPowerUp.ObjectName = "PowerUp";
        existingPowerUp.ImpactStatus!.HasExploded = false;

        var speedCarrier = CreateExplodedObject(33);
        speedCarrier.ObjectName = "Seeder";
        speedCarrier.HasPowerUp = false;
        speedCarrier.PowerUpType = PowerUpType.Standard;
        speedCarrier.WorldPosition = new Vector3 { x = 100f, z = 200f };

        GameState.SurfaceState.AiObjects = new List<_3dObject> { existingPowerUp, speedCarrier };
        var world = new TestWorld
        {
            WorldInhabitants = new List<I3dObject> { existingPowerUp, speedCarrier }
        };

        InvokePrivate(new LiveGameLoop(), "CleanupExplodedObjects", world);

        var drops = world.WorldInhabitants.OfType<_3dObject>()
            .Where(obj => obj.ObjectName == "PowerUp")
            .ToList();
        Assert.AreEqual(2, drops.Count);
        Assert.IsTrue(drops.Any(obj => obj.PowerUpType == PowerUpType.TravelSpeedLevel1),
            "A standard pickup waiting in the world must not discard the one-off speed pickup.");
    }

    [TestMethod]
    public void CleanupExplodedObjects_TutorialSeederKillDoesNotConsumePowerUpDrop()
    {
        _3dRotations.Helpers.PowerUpDropPolicy.ConfigureForWave(totalSeeders: 1, powerUpCount: 1);
        GameState.SurfaceState.GlobalMapPosition = new Vector3();
        GameState.SurfaceState.AiObjects = new List<_3dObject>();
        GameState.GamePlayState.CurrentSceneType = SceneTypes.Tutorial;

        var explodedSeeder = CreateExplodedObject(31);
        explodedSeeder.ObjectName = "Seeder";
        explodedSeeder.HasPowerUp = false;
        explodedSeeder.WorldPosition = new Vector3();
        explodedSeeder.ObjectOffsets = new Vector3();
        GameState.SurfaceState.AiObjects.Add(explodedSeeder);

        var world = new TestWorld
        {
            WorldInhabitants = new List<I3dObject> { explodedSeeder }
        };

        var loop = new LiveGameLoop();
        InvokePrivate(loop, "CleanupExplodedObjects", world);

        Assert.AreEqual(0, _3dRotations.Helpers.PowerUpDropPolicy.SeederKillsObserved,
            "Tutorial seeder kills must not advance the wave-wide PowerUpDropPolicy counter.");
    }

    [TestMethod]
    public void CleanupExplodedObjects_SeederWithPowerUpDropDoesNotSaveCheckpoint()
    {
        // Checkpoints belong to the pickup, not the drop. A killed seeder that drops a
        // powerup must NOT trigger a checkpoint save here; the checkpoint is written by
        // ShipControls.CollectPowerUp when the player actually grabs it.
        _3dRotations.Helpers.PowerUpDropPolicy.ConfigureForWave(totalSeeders: 1, powerUpCount: 1);
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 1;
        gps.CurrentSceneType = SceneTypes.Game;
        gps.HasCheckpoint = false;
        GameState.SurfaceState.GlobalMapPosition = new Vector3();
        GameState.SurfaceState.AiObjects = new List<_3dObject>();

        var explodedSeeder = CreateExplodedObject(50);
        explodedSeeder.ObjectName = "Seeder";
        explodedSeeder.HasPowerUp = false; // policy will promote on kill
        explodedSeeder.WorldPosition = new Vector3 { x = 100f, y = 0f, z = 200f };
        explodedSeeder.ObjectOffsets = new Vector3 { x = 0f, y = -200f, z = 600f };
        GameState.SurfaceState.AiObjects.Add(explodedSeeder);

        var world = new TestWorld
        {
            WorldInhabitants = new List<I3dObject> { explodedSeeder }
        };

        var loop = new LiveGameLoop();
        InvokePrivate(loop, "CleanupExplodedObjects", world);

        bool spawnedPowerUp = world.WorldInhabitants.OfType<_3dObject>().Any(x => x.ObjectName == "PowerUp");
        Assert.IsTrue(spawnedPowerUp,
            "Test precondition: seeder kill must have produced a PowerUp drop.");
        Assert.IsFalse(gps.HasCheckpoint,
            "Dropping a powerup must NOT save a checkpoint; the checkpoint is owned by the pickup event.");
    }

    [TestMethod]
    public void CleanupExplodedObjects_MotherShipKillSavesCheckpointAndHighscore()
    {
        var gps = GameState.GamePlayState;
        gps.PlayerName = "Pilot";
        gps.SceneIndex = 6;
        gps.CurrentSceneType = SceneTypes.Game;
        gps.Score = 43210;
        gps.TotalKills = 12;
        gps.TotalShotsFired = 24;
        gps.Lives = 2;
        gps.Health = 71f;
        gps.InitialMotherShips = 1;
        gps.MotherShipsRemaining = 1;

        var explodedMotherShip = CreateExplodedObject(40);
        explodedMotherShip.ObjectName = "MotherShipSmall";
        explodedMotherShip.IsActive = true;

        GameState.SurfaceState.AiObjects = new List<_3dObject> { explodedMotherShip };
        var world = new TestWorld
        {
            WorldInhabitants = new List<I3dObject> { explodedMotherShip }
        };

        var loop = new LiveGameLoop();

        InvokePrivate(loop, "CleanupExplodedObjects", world);

        Assert.IsFalse(world.WorldInhabitants.Any(x => x.ObjectId == 40));
        Assert.IsTrue(gps.HasCheckpoint, "Killing a mothership should capture a checkpoint.");
        Assert.AreEqual(0, gps.CheckpointMotherShipsRemaining);

        var saved = GameStatePersistence.LoadGameState("Pilot");
        Assert.IsNotNull(saved);
        Assert.IsTrue(saved!.HasCheckpoint);
        Assert.AreEqual(gps.Score, saved.Score);
        Assert.AreEqual(0, saved.MotherShipsRemaining);

        var highscores = HighscoreService.LoadLocalHighscores();
        Assert.AreEqual(1, highscores.Entries.Count);
        Assert.AreEqual(gps.Score, highscores.Entries[0].Score);
    }

    [TestMethod]
    public void BeginVictoryRewardOverlay_DisablesInputDismissal()
    {
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.GamePlayState = new GamePlayState
        {
            CurrentSceneType = SceneTypes.Game,
            TotalBioTiles = 100,
            InfectionLevel = 10f,
            Health = 80f,
            MaxHealth = 100f
        };

        var loop = new LiveGameLoop();
        var world = new TestWorld();

        InvokePrivate(loop, "BeginVictoryRewardOverlay", world, SceneTypes.Game);

        var overlay = GameState.ScreenOverlayState;
        Assert.AreEqual(ScreenOverlayType.Game, overlay.Type);
        Assert.IsTrue(overlay.ShowOverlay);
        Assert.AreEqual("PLANET SECURED", overlay.Header);
        Assert.IsFalse(overlay.CanDismissWithInput,
            "Victory reward overlay is time-driven and must not be dismissed by mouse/controller activation.");
        Assert.IsTrue(GameState.GamePlayState.IsVictoryRewardPauseActive,
            "Victory reward should pause gameplay while the bonus is counted.");
        Assert.IsTrue(world.IsPaused,
            "Victory reward should use the existing world pause path so the last rendered frame stays frozen.");
        Assert.AreEqual(GamePhase.Paused, GameState.GamePlayState.Phase);

        InvokePrivate(loop, "ClearVictoryRewardState");

        Assert.IsFalse(GameState.GamePlayState.IsVictoryRewardPauseActive);
    }

    [TestMethod]
    public void UpdateWorld_WhenVictoryRewardPauseIsActive_DoesNotMoveObjects()
    {
        GameState.GamePlayState.IsVictoryRewardPauseActive = true;

        var surfaceMovement = new CountingMovement();
        var surfaceBasedMovement = new CountingMovement();
        var worldMovement = new CountingMovement();
        var surface = new TestSurface();

        var world = new TestWorld
        {
            WorldInhabitants = new List<I3dObject>
            {
                CreateRenderableObject(101, "Surface", surfaceMovement, parentSurface: surface),
                CreateRenderableObject(102, "Tree", surfaceBasedMovement, surfaceBasedId: 77),
                CreateRenderableObject(103, "KamikazeDrone", worldMovement)
            }
        };

        var loop = new LiveGameLoop();
        var projected = new List<_2dTriangleMesh>();
        var crashBoxes = new List<_2dTriangleMesh>();

        loop.UpdateWorld(world, ref projected, ref crashBoxes);

        Assert.AreEqual(0, surfaceMovement.MoveCount,
            "Surface movement must not rebuild the viewport while victory reward pause is active.");
        Assert.AreEqual(0, surfaceBasedMovement.MoveCount,
            "Surface-based objects must not resync while victory reward pause is active.");
        Assert.AreEqual(0, worldMovement.MoveCount,
            "Free world/AI objects must remain frozen during the victory reward pause.");
    }

    [TestMethod]
    public void UpdatePausedVictoryReward_WhenRewardTimerCompletes_UnpausesWorldAndRequestsFadeOut()
    {
        GameState.GamePlayState = new GamePlayState
        {
            CurrentSceneType = SceneTypes.Game,
            TotalBioTiles = 100,
            InfectionLevel = 10f,
            Health = 80f,
            MaxHealth = 100f
        };

        var world = new TestWorld();
        var loop = new LiveGameLoop();

        InvokePrivate(loop, "BeginVictoryRewardOverlay", world, SceneTypes.Game);
        SetPrivate(loop, "_victorySequenceStarted", true);
        SetPrivate(loop, "_victoryStartTicks", Stopwatch.GetTimestamp() - Stopwatch.Frequency * 10);

        loop.UpdatePausedVictoryReward(world);

        Assert.IsFalse(world.IsPaused,
            "When the reward timer finishes, the world must unpause so the existing fade/reset path can complete.");
        Assert.AreEqual(GamePhase.Playing, GameState.GamePlayState.Phase);
        Assert.IsTrue(GameState.WorldFade.IsFadeOutPendingOrActive);
        Assert.IsTrue(GameState.GamePlayState.IsVictoryRewardPauseActive,
            "The victory flag stays active through fade-out so gameplay input and movement remain blocked until scene reset.");
    }

    [TestMethod]
    public void UpdatePausedVictoryReward_WhenRewardTimerCompletes_AppliesRewardPointsToScoreOnce()
    {
        GameState.GamePlayState = new GamePlayState
        {
            PlayerName = "RewardPilot",
            SceneIndex = 3,
            CurrentSceneType = SceneTypes.Game,
            Score = 1000,
            TotalBioTiles = 100,
            InfectionLevel = 25f,
            Health = 50f,
            MaxHealth = 100f,
            Lives = 2,
            TotalShotsFired = 10,
            TotalKills = 5,
            HasPlanetStartSnapshot = true,
            PlanetStartTotalDeaths = 1,
            TotalDeaths = 1,
            InitialMotherShips = 1,
            MotherShipsRemaining = 0,
            PlanetStyleBonusScore = 300,
            HasCheckpoint = true,
            CheckpointScore = 1000,
            CheckpointSceneIndex = 3,
            CheckpointPlanetStyleBonusScore = 300,
            CheckpointPlanetStyleBonusSceneIndex = 3
        };
        long startingScore = GameState.GamePlayState.Score;
        int expectedReward = PlanetRewardCalculator.Calculate(GameState.GamePlayState).TotalPoints;
        var world = new TestWorld();
        var loop = new LiveGameLoop();

        InvokePrivate(loop, "BeginVictoryRewardOverlay", world, SceneTypes.Game);
        SetPrivate(loop, "_victorySequenceStarted", true);
        SetPrivate(loop, "_victoryStartTicks", Stopwatch.GetTimestamp() - Stopwatch.Frequency * 10);

        loop.UpdatePausedVictoryReward(world);
        long scoreAfterFirstUpdate = GameState.GamePlayState.Score;
        loop.UpdatePausedVictoryReward(world);

        Assert.AreEqual(startingScore + expectedReward, scoreAfterFirstUpdate,
            "Completing the victory reward count-up must add the calculated planet reward to the live score.");
        Assert.AreEqual(scoreAfterFirstUpdate, GameState.GamePlayState.Score,
            "Reward points must not be applied more than once while fade-out is pending.");
        Assert.AreEqual(scoreAfterFirstUpdate, GameState.GamePlayState.CheckpointScore,
            "Reward points must also update checkpoint score so checkpoint persistence cannot lose them.");

        GameStatePersistence.SaveGameState();
        var loaded = GameStatePersistence.LoadGameState("RewardPilot");
        Assert.IsNotNull(loaded);
        Assert.AreEqual(scoreAfterFirstUpdate, loaded!.Score);
        Assert.AreEqual(scoreAfterFirstUpdate, loaded.CheckpointScore);

        GameState.GamePlayState = new GamePlayState();
        GameStatePersistence.RestoreToGamePlayState(loaded);
        Assert.AreEqual(scoreAfterFirstUpdate, GameState.GamePlayState.Score,
            "Loading a checkpoint save must restore the reward-adjusted score.");
    }

    private static Vector3 Copy(IVector3 source)
    {
        return new Vector3
        {
            x = source.x,
            y = source.y,
            z = source.z
        };
    }

    private static _3dObject CreateExplodedObject(int objectId)
    {
        return new _3dObject
        {
            ObjectId = objectId,
            ObjectName = "Decoration",
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            CrashBoxes = new List<List<IVector3>>(),
            ObjectParts = new List<I3dObjectPart>(),
            ImpactStatus = new ImpactStatus { HasExploded = true }
        };
    }

    private static _3dObject CreateRenderableObject(
        int objectId,
        string objectName,
        IObjectMovement movement,
        int? surfaceBasedId = null,
        ISurface? parentSurface = null)
    {
        return new _3dObject
        {
            ObjectId = objectId,
            ObjectName = objectName,
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            SurfaceBasedId = surfaceBasedId,
            CrashBoxes = new List<List<IVector3>>(),
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = objectName + "Body",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "ffffff",
                            vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 10f, y = 0f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = 10f, z = 0f },
                            normal1 = new Vector3(),
                            normal2 = new Vector3(),
                            normal3 = new Vector3()
                        }
                    }
                }
            },
            Movement = movement,
            ParentSurface = parentSurface,
            ImpactStatus = new ImpactStatus(),
            IsActive = true
        };
    }

    private static void InvokePrivate(LiveGameLoop loop, string methodName, params object?[] args)
    {
        var method = typeof(LiveGameLoop).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Expected private method '{methodName}' to exist.");
        method.Invoke(loop, args);
    }

    private static void SetPrivate(LiveGameLoop loop, string fieldName, object value)
    {
        var field = typeof(LiveGameLoop).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
        field.SetValue(loop, value);
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = new TestSceneHandler();
        public IGameEventBus? EventBus { get; set; }
        public bool IsPaused { get; set; }
    }

    private sealed class TestSceneHandler : ISceneHandler
    {
        public void SetupActiveScene(I3dWorld world) { }
        public void ResetActiveScene(I3dWorld world) { }
        public void ResetActiveSceneToPlanetStart(I3dWorld world) { }
        public void NextScene(I3dWorld world) { }
        public IScene GetActiveScene() => null!;
        public void HandleKeyPress(GameInputKey key, I3dWorld world) { }
        public void HandleOverlayActivation(I3dWorld world) { }
        public void UpdateFrame(I3dWorld world) { }
    }

    private sealed class CountingMovement : IObjectMovement
    {
        public int MoveCount { get; private set; }
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = null!;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            MoveCount++;
            return theObject;
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void Dispose() { }
    }

    private sealed class TestSurface : ISurface
    {
        public Vector3 GlobalMapRotation { get; set; } = new();
        public List<ITriangleMeshWithColor> RotatedSurfaceTriangles { get; set; } = new();
        public Dictionary<long, ITriangleMeshWithColor> RotatedSurfaceTriangleByLandId { get; set; } = new();
        public HashSet<long?> LandBasedIds { get; set; } = new();

        public int SurfaceWidth() => 0;
        public int GlobalMapSize() => 0;
        public int ViewPortSize() => 0;
        public int TileSize() => 1;
        public int MaxHeight() => 1;
        public I3dObject GetSurfaceViewPort() => CreateRenderableObject(999, "Surface", new CountingMovement(), parentSurface: this);
        public void Create2DMap(int? maxTrees, int? maxHouses, GameModes gameMode, string? recordedSurface) { }
    }
}
