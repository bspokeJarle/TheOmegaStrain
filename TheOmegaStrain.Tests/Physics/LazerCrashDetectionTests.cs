using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Runtime.Collision;

namespace TheOmegaStrain.Tests.Physics;

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
            AiObjects = new List<OmegaObject3D>(),
            GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f }
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        OmegaWorldViewSetup.ConfigurePitch(63f);
    }

    [DataTestMethod]
    [DataRow(CameraAnglePreset.Low)]
    [DataRow(CameraAnglePreset.Normal)]
    [DataRow(CameraAnglePreset.High)]
    public void Ship_CollidesWithSurfaceAtCrashCenterRotatedForCameraAngle(CameraAnglePreset angle)
    {
        var settings = new GameSettingsState { CameraAngle = angle };
        OmegaWorldViewSetup.ConfigurePitch(settings.CameraPitchDegrees);

        var ship = CreateCrashObject("Ship", 9001);
        ship.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 0f };
        ship.CrashBoxes = new List<List<IVector3>>
        {
            OmegaObject3DHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -8f, y = -8f, z = 72f },
                new Vector3 { x = 8f, y = 8f, z = 88f })
        };
        ship.CrashBoxesFollowRotation = true;
        new ObjectFrameTransformer().RotateObjectGeometry(ship);

        var surface = CreateCrashObject("Surface", 9002);
        surface.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 0f };
        surface.CrashBoxes = new List<List<IVector3>>
        {
            OmegaObject3DHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -8f, y = -8f, z = 72f },
                new Vector3 { x = 8f, y = 8f, z = 88f })
        };
        surface.CrashBoxesFollowRotation = true;
        new ObjectFrameTransformer().RotateObjectGeometry(surface);

        var rotatedCenter = Center(ship.CrashBoxes[0]);
        Assert.AreEqual(80f * MathF.Sin(settings.CameraPitchDegrees * MathF.PI / 180f), MathF.Abs(rotatedCenter.y), 0.01f);
        Assert.AreEqual(80f * MathF.Cos(settings.CameraPitchDegrees * MathF.PI / 180f), MathF.Abs(rotatedCenter.z), 0.01f);

        ship.ObjectOffsets = new Vector3 { x = -rotatedCenter.x, y = -rotatedCenter.y, z = -rotatedCenter.z };
        surface.ObjectOffsets = new Vector3 { x = -rotatedCenter.x, y = -rotatedCenter.y, z = -rotatedCenter.z };

        bool collided = CollisionBoxScanner.TryFindFirstBoxCollision(
            ship,
            surface,
            new CollisionMargins(0f, 0f, 0f),
            static (obj, _, box) =>
            {
                var offset = CrashBoxTransform.GetEffectiveCrashOffset(
                    obj,
                    static (x, y, z) => new Vector3 { x = x, y = y, z = z });
                return CrashBoxTransform.ToCrashWorldPoints(
                    box,
                    offset,
                    static (x, y, z) => new Vector3 { x = x, y = y, z = z });
            },
            out _);

        Assert.IsTrue(collided, $"Crash detection should find the rotated ship/surface overlap for {angle}.");
        Assert.AreEqual(settings.CameraPitchDegrees, ship.Rotation.x, 0.001f);
    }

    [DataTestMethod]
    [DataRow("EnemyRocket", "Ship", true)]
    [DataRow("EnemyRocket", "AttackShip", false)]
    [DataRow("EnemyRocket", "KamikazeDrone", false)]
    [DataRow("Rocket", "Ship", false)]
    [DataRow("EnemyRocket", "Particle", false)]
    [DataRow("Rocket", "Particle", false)]
    public void RocketCollision_RespectsWeaponOwnership(string rocketName, string targetName, bool shouldHit)
    {
        var rocket = CreateCrashObject(rocketName, 9501);
        var target = CreateCrashObject(targetName, 9502);
        CrashDetection.HandleCrashboxes(new List<OmegaObject3D> { rocket, target }, isPaused: false);
        CrashDetection.HandleCrashboxes(new List<OmegaObject3D> { rocket, target }, isPaused: false);
        Assert.AreEqual(shouldHit, rocket.ImpactStatus!.HasCrashed);
        Assert.AreEqual(shouldHit, target.ImpactStatus!.HasCrashed);
        if (shouldHit) Assert.AreEqual("EnemyRocket", target.ImpactStatus.ObjectName);
    }

    [TestMethod]
    public void PlayerLazer_CollidesWithEnemy()
    {
        var lazer = CreateCrashObject("Lazer", 9101);
        var enemy = CreateCrashObject("KamikazeDrone", 9102);

        CrashDetection.HandleCrashboxes(new List<OmegaObject3D> { lazer, enemy }, isPaused: false);

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

        CrashDetection.HandleCrashboxes(new List<OmegaObject3D> { lazer, enemy }, isPaused: false);

        Assert.IsFalse(lazer.ImpactStatus!.HasCrashed, "Enemy lazer should not collide with enemies.");
        Assert.IsFalse(enemy.ImpactStatus!.HasCrashed, "Enemies should ignore enemy lazer collisions.");
    }

    [TestMethod]
    public void EnemyLazer_CollidesWithShip()
    {
        var lazer = CreateCrashObject("EnemyLazerMedium", 9301);
        var ship = CreateCrashObject("Ship", 9302);

        CrashDetection.HandleCrashboxes(new List<OmegaObject3D> { lazer, ship }, isPaused: false);

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

        CrashDetection.HandleCrashboxes(new List<OmegaObject3D> { lazer, ship }, isPaused: false);

        Assert.IsFalse(lazer.ImpactStatus!.HasCrashed,
            "Suppression after overlay resume should skip collision pairs involving Ship.");
        Assert.IsFalse(ship.ImpactStatus!.HasCrashed,
            "Ship should not receive crash state during the short overlay resume grace window.");
    }

    private static OmegaObject3D CreateCrashObject(string objectName, int objectId)
    {
        return new OmegaObject3D
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
            OmegaObject3DHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -10f, y = -10f, z = -10f },
                new Vector3 { x = 10f, y = 10f, z = 10f })
        };
    }

    private static List<I3dObjectPart> CreateObjectParts()
    {
        return new List<I3dObjectPart>
        {
            new OmegaObjectPart3D
            {
                PartName = "Body",
                IsVisible = true,
                Triangles = new List<ITriangleMeshWithColorAndTexture>
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

    private static Vector3 Center(IReadOnlyList<IVector3> points)
    {
        return new Vector3
        {
            x = points.Average(point => point.x),
            y = points.Average(point => point.y),
            z = points.Average(point => point.z)
        };
    }
}
