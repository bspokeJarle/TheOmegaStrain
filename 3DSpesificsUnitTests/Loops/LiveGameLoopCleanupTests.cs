using _3dTesting.MainWindowClasses.Loops;
using CommonUtilities._3DHelpers;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.Events;
using CommonUtilities.Persistence;
using Domain;
using System.Reflection;
using System.Windows.Input;
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

    private static void InvokePrivate(LiveGameLoop loop, string methodName, params object?[] args)
    {
        var method = typeof(LiveGameLoop).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Expected private method '{methodName}' to exist.");
        method.Invoke(loop, args);
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
        public void HandleKeyPress(KeyEventArgs k, I3dWorld world) { }
        public void HandleOverlayActivation(I3dWorld world) { }
        public void UpdateFrame(I3dWorld world) { }
    }
}
