using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using _3dRotations.Helpers;
using TheOmegaStrain.Runtime.Collision;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Physics;

[TestClass]
public class LazerCrashDetectionTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.ShipState = new ShipState();
        GameState.SurfaceState = new SurfaceState
        {
            AiObjects = new List<_3dObject>(),
            GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f }
        };
    }

    [TestMethod]
    public void PlayerLazer_CollidesWithEnemy()
    {
        var lazer = CreateCrashObject("Lazer", 9101);
        var enemy = CreateCrashObject("KamikazeDrone", 9102);

        CrashDetection.HandleCrashboxes(new List<_3dObject> { lazer, enemy }, isPaused: false);

        Assert.IsTrue(lazer.ImpactStatus!.HasCrashed, "Player lazer should be allowed to hit enemies.");
        Assert.IsTrue(enemy.ImpactStatus!.HasCrashed, "Enemy should receive the player lazer collision.");
        Assert.AreEqual("KamikazeDrone", lazer.ImpactStatus.ObjectName);
        Assert.AreEqual("Lazer", enemy.ImpactStatus.ObjectName);
    }

    [TestMethod]
    public void EnemyLazer_DoesNotCollideWithEnemy()
    {
        var lazer = CreateCrashObject("EnemyLazerMedium", 9201);
        var enemy = CreateCrashObject("KamikazeDrone", 9202);

        CrashDetection.HandleCrashboxes(new List<_3dObject> { lazer, enemy }, isPaused: false);

        Assert.IsFalse(lazer.ImpactStatus!.HasCrashed, "Enemy lazer should not collide with enemies.");
        Assert.IsFalse(enemy.ImpactStatus!.HasCrashed, "Enemies should ignore enemy lazer collisions.");
    }

    [TestMethod]
    public void EnemyLazer_CollidesWithShip()
    {
        var lazer = CreateCrashObject("EnemyLazerMedium", 9301);
        var ship = CreateCrashObject("Ship", 9302);

        CrashDetection.HandleCrashboxes(new List<_3dObject> { lazer, ship }, isPaused: false);

        Assert.IsTrue(lazer.ImpactStatus!.HasCrashed, "Enemy lazer should still be allowed to hit the player ship.");
        Assert.IsTrue(ship.ImpactStatus!.HasCrashed, "Ship should receive enemy lazer collisions.");
        Assert.AreEqual("Ship", lazer.ImpactStatus.ObjectName);
        Assert.AreEqual("EnemyLazerMedium", ship.ImpactStatus.ObjectName);
    }

    [TestMethod]
    public void ShipCrashDetectionSuppressed_SkipsShipCollisionsTemporarily()
    {
        GameState.ShipState.ShipCrashDetectionDisabledUntilUtc = DateTime.UtcNow.AddSeconds(2);
        var lazer = CreateCrashObject("EnemyLazerMedium", 9401);
        var ship = CreateCrashObject("Ship", 9402);

        CrashDetection.HandleCrashboxes(new List<_3dObject> { lazer, ship }, isPaused: false);

        Assert.IsFalse(lazer.ImpactStatus!.HasCrashed,
            "Suppression after overlay resume should skip collision pairs involving Ship.");
        Assert.IsFalse(ship.ImpactStatus!.HasCrashed,
            "Ship should not receive crash state during the short overlay resume grace window.");
    }

    private static _3dObject CreateCrashObject(string objectName, int objectId)
    {
        return new _3dObject
        {
            ObjectId = objectId,
            ObjectName = objectName,
            IsOnScreen = true,
            WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f },
            ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 0f },
            Rotation = new Vector3 { x = 0f, y = 0f, z = 0f },
            CrashBoxes = CreateCrashBoxes(),
            ImpactStatus = new ImpactStatus { ObjectName = objectName, ObjectHealth = 100 },
            ObjectParts = CreateObjectParts()
        };
    }

    private static List<List<IVector3>> CreateCrashBoxes()
    {
        return new List<List<IVector3>>
        {
            _3dObjectHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -10f, y = -10f, z = -10f },
                new Vector3 { x = 10f, y = 10f, z = 10f })
        };
    }

    private static List<I3dObjectPart> CreateObjectParts()
    {
        return new List<I3dObjectPart>
        {
            new _3dObjectPart
            {
                PartName = "Body",
                IsVisible = true,
                Triangles = new List<ITriangleMeshWithColor>
                {
                    new TriangleMeshWithColor
                    {
                        Color = "ffffff",
                        vert1 = new Vector3 { x = -5f, y = 0f, z = 0f },
                        vert2 = new Vector3 { x = 5f, y = 0f, z = 0f },
                        vert3 = new Vector3 { x = 0f, y = 5f, z = 0f },
                        normal1 = new Vector3 { x = 0f, y = 0f, z = 1f }
                    }
                }
            }
        };
    }
}
