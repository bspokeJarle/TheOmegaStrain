using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.Persistence;
using Domain;
using GameAiAndControls.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Persistence;

[TestClass]
public class PowerUpCheckpointPersistenceTests
{
    private string _originalLocalFolder = string.Empty;
    private string _testLocalFolder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainPowerUpTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.Initialize();

        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ShipState = new ShipState();

        GameState.GamePlayState.PlayerName = "Jarle";
        GameState.SurfaceState.AiObjects = new List<_3dObject>();
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
    public void CollectingPowerUp_SavesCheckpointAndPersistsPowerUpsCollected()
    {
        var gps = GameState.GamePlayState;
        gps.PowerUpsCollected = 0;
        gps.Score = 0;
        gps.HasCheckpoint = false;

        var ship = new _3dObject
        {
            ObjectId = 1,
            ObjectName = "Ship",
            ImpactStatus = new ImpactStatus { HasCrashed = true, ObjectName = "PowerUp", ObjectHealth = 100 },
            ObjectParts = new List<I3dObjectPart> { new _3dObjectPart { PartName = "Ship", Triangles = new List<ITriangleMeshWithColor>() } },
            CrashBoxes = new List<List<IVector3>>(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Movement = new ShipControls()
        };

        var powerUp = new _3dObject
        {
            ObjectId = 2,
            ObjectName = "PowerUp",
            ImpactStatus = new ImpactStatus { HasCrashed = true },
            ObjectParts = new List<I3dObjectPart> { new _3dObjectPart { PartName = "PowerUp", Triangles = new List<ITriangleMeshWithColor>() } },
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3()
        };

        GameState.SurfaceState.AiObjects.Add(powerUp);

        // Trigger ship collision handling path that collects powerup.
        ((ShipControls)ship.Movement!).MoveObject(ship, null, null);

        Assert.AreEqual(1, gps.PowerUpsCollected, "PowerUp collection should increment PowerUpsCollected.");
        Assert.IsTrue(gps.HasCheckpoint, "PowerUp collection should save a checkpoint.");
        Assert.AreEqual(1, gps.CheckpointPowerUpsCollected,
            "Checkpoint should capture collected powerups immediately.");

        var loaded = GameStatePersistence.LoadGameState("Jarle");
        Assert.IsNotNull(loaded, "PowerUp collection should persist save file.");
        Assert.AreEqual(1, loaded!.PowerUpsCollected,
            "Saved game should retain collected powerups.");
        Assert.IsTrue(loaded.HasCheckpoint,
            "Saved game should retain checkpoint after powerup collection.");

        var highscores = HighscoreService.LoadLocalHighscores();
        Assert.AreEqual(1, highscores.Entries.Count,
            "PowerUp checkpoints should persist the current highscore too.");
        Assert.AreEqual(gps.Score, highscores.Entries[0].Score);
    }

    [TestMethod]
    public void CollectingSamePowerUpTwice_CountsOnlyOnce()
    {
        var gps = GameState.GamePlayState;
        gps.PowerUpsCollected = 0;
        gps.Score = 0;

        var controls = new ShipControls();
        try
        {
            var ship = CreatePowerUpHitShip(31, controls);
            var powerUp = CreateCrashedPowerUp(32);
            powerUp.CrashBoxes = new List<List<IVector3>>
            {
                new() { new Vector3(), new Vector3 { x = 1f, y = 1f, z = 1f } }
            };
            GameState.SurfaceState.AiObjects.Add(powerUp);

            controls.MoveObject(ship, null, null);

            ship.ImpactStatus!.HasCrashed = true;
            ship.ImpactStatus.ObjectName = "PowerUp";
            controls.MoveObject(ship, null, null);

            Assert.AreEqual(1, gps.PowerUpsCollected,
                "The same PowerUp object must not advance unlock progression more than once.");
            Assert.IsFalse(gps.IsLazerUnlocked,
                "A single collected PowerUp must unlock Decoy only; Lazer requires a second distinct pickup.");
            Assert.AreEqual(0, powerUp.CrashBoxes.Count,
                "Collected PowerUps should drop their crashboxes immediately to avoid repeat collision frames.");
        }
        finally
        {
            controls.Dispose();
        }
    }

    [TestMethod]
    public void CollectingPowerUp_InTutorialDoesNotSaveCheckpointOrPersistPowerup()
    {
        var gps = GameState.GamePlayState;
        gps.CurrentSceneType = SceneTypes.Tutorial;
        gps.PowerUpsCollected = 0;
        gps.Score = 0;
        gps.HasCheckpoint = false;

        var ship = new _3dObject
        {
            ObjectId = 21,
            ObjectName = "Ship",
            ImpactStatus = new ImpactStatus { HasCrashed = true, ObjectName = "PowerUp", ObjectHealth = 100 },
            ObjectParts = new List<I3dObjectPart> { new _3dObjectPart { PartName = "Ship", Triangles = new List<ITriangleMeshWithColor>() } },
            CrashBoxes = new List<List<IVector3>>(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Movement = new ShipControls()
        };

        GameState.SurfaceState.AiObjects.Add(new _3dObject
        {
            ObjectId = 22,
            ObjectName = "PowerUp",
            IsActive = true,
            ImpactStatus = new ImpactStatus { HasCrashed = true, HasExploded = false },
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>()
        });

        ((ShipControls)ship.Movement!).MoveObject(ship, null, null);

        Assert.AreEqual(1, gps.PowerUpsCollected,
            "Tutorial still needs a temporary powerup count to unlock decoy during training.");
        Assert.AreEqual(0, gps.Score,
            "Tutorial powerups should not award campaign/highscore points.");
        Assert.IsFalse(gps.HasCheckpoint,
            "Tutorial powerups should not create campaign checkpoints.");
        Assert.IsNull(GameStatePersistence.LoadGameState("Jarle"),
            "Tutorial powerups should not persist a campaign save.");
    }

    [TestMethod]
    public void CollectingPowerUp_CheckpointCapturesCurrentEnemyCounts_AndDoesNotForceMothershipPhase()
    {
        var gps = GameState.GamePlayState;
        gps.PowerUpsCollected = 0;
        gps.SeedersRemaining = 0;
        gps.DronesRemaining = 0;
        gps.MotherShipsRemaining = 0;

        var ship = new _3dObject
        {
            ObjectId = 11,
            ObjectName = "Ship",
            ImpactStatus = new ImpactStatus { HasCrashed = true, ObjectName = "PowerUp", ObjectHealth = 100 },
            ObjectParts = new List<I3dObjectPart> { new _3dObjectPart { PartName = "Ship", Triangles = new List<ITriangleMeshWithColor>() } },
            CrashBoxes = new List<List<IVector3>>(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Movement = new ShipControls()
        };

        // Existing active enemies in current scene state
        GameState.SurfaceState.AiObjects.Add(new _3dObject
        {
            ObjectId = 12,
            ObjectName = "Seeder",
            IsActive = true,
            ImpactStatus = new ImpactStatus { HasExploded = false },
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>()
        });

        GameState.SurfaceState.AiObjects.Add(new _3dObject
        {
            ObjectId = 13,
            ObjectName = "KamikazeDrone",
            IsActive = true,
            ImpactStatus = new ImpactStatus { HasExploded = false },
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>()
        });

        GameState.SurfaceState.AiObjects.Add(new _3dObject
        {
            ObjectId = 14,
            ObjectName = "MotherShipSmall",
            IsActive = false,
            ImpactStatus = new ImpactStatus { HasExploded = false },
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>()
        });

        GameState.SurfaceState.AiObjects.Add(new _3dObject
        {
            ObjectId = 15,
            ObjectName = "PowerUp",
            IsActive = true,
            ImpactStatus = new ImpactStatus { HasCrashed = true, HasExploded = false },
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>()
        });

        ((ShipControls)ship.Movement!).MoveObject(ship, null, null);

        Assert.IsTrue(gps.HasCheckpoint);
        Assert.AreEqual(1, gps.CheckpointSeedersRemaining,
            "Checkpoint should keep current seeder count on powerup pickup.");
        Assert.AreEqual(1, gps.CheckpointDronesRemaining,
            "Checkpoint should keep current active drone count on powerup pickup.");
        Assert.AreEqual(0, gps.CheckpointMotherShipsRemaining,
            "Checkpoint should not force mothership phase while other enemies remain.");
    }

    [TestMethod]
    public void CollectingSpeedPowerUp_PersistsSpeedWithoutAdvancingWeaponTier()
    {
        var gps = GameState.GamePlayState;
        var controls = new ShipControls();
        try
        {
            var ship = CreatePowerUpHitShip(41, controls);
            var powerUp = CreateCrashedPowerUp(42);
            powerUp.PowerUpType = PowerUpType.TravelSpeedLevel1;
            GameState.SurfaceState.AiObjects.Add(powerUp);

            controls.MoveObject(ship, null, null);

            Assert.AreEqual(1, gps.SpeedPowerUpLevel);
            Assert.AreEqual(1.20f, gps.TravelSpeedMultiplier);
            Assert.AreEqual(0, gps.PowerUpsCollected);
            Assert.IsFalse(gps.IsDecoyUnlocked);
            Assert.AreEqual(1, gps.CheckpointSpeedPowerUpLevel);

            var loaded = GameStatePersistence.LoadGameState("Jarle");
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded!.SpeedPowerUpLevel);
            Assert.AreEqual(1, loaded.CheckpointSpeedPowerUpLevel);
        }
        finally
        {
            controls.Dispose();
        }
    }

    private static _3dObject CreatePowerUpHitShip(int objectId, ShipControls controls)
    {
        return new _3dObject
        {
            ObjectId = objectId,
            ObjectName = "Ship",
            ImpactStatus = new ImpactStatus { HasCrashed = true, ObjectName = "PowerUp", ObjectHealth = 100 },
            ObjectParts = new List<I3dObjectPart> { new _3dObjectPart { PartName = "Ship", Triangles = new List<ITriangleMeshWithColor>() } },
            CrashBoxes = new List<List<IVector3>>(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Movement = controls
        };
    }

    private static _3dObject CreateCrashedPowerUp(int objectId)
    {
        return new _3dObject
        {
            ObjectId = objectId,
            ObjectName = "PowerUp",
            IsActive = true,
            ImpactStatus = new ImpactStatus { HasCrashed = true, HasExploded = false },
            WorldPosition = new Vector3(),
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>()
        };
    }
}
