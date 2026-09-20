using RetroMesh.Engine;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.World.Objects;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class ShieldPowerUpTests
{
    [TestMethod]
    public void Shield_ReducesDamageToTwentyPercentForThirtySeconds()
    {
        var gameplay = new GamePlayState();

        gameplay.ActivateShield();

        Assert.AreEqual(GameSetup.ShieldDurationSeconds, gameplay.ShieldSecondsLeft, 0.001f);
        Assert.AreEqual(20, gameplay.CalculateIncomingDamage(100));
        Assert.AreEqual(1f, gameplay.ShieldRemainingFraction, 0.001f);

        gameplay.ApplyDamage(50f, invulnerableSeconds: 0f);
        Assert.AreEqual(90f, gameplay.Health, 0.001f);

        gameplay.Update(29f);
        Assert.AreEqual(20, gameplay.CalculateIncomingDamage(100));
        Assert.AreEqual(1f / 30f, gameplay.ShieldRemainingFraction, 0.001f);

        gameplay.Update(1f);
        Assert.IsFalse(gameplay.IsShieldActive);
        Assert.AreEqual(100, gameplay.CalculateIncomingDamage(100));
    }

    [TestMethod]
    public void SpaceSwans_ShieldCarrierRatioFallsFromThirtyToFifteenPercentAcrossCampaign()
    {
        var objects = new List<I3dObject>();
        for (int i = 0; i < 100; i++)
            objects.Add(new OmegaObject3D { ObjectId = i, ObjectName = "SpaceSwan" });

        int firstSceneAssigned = ShieldPowerUpPlacementHelpers.AssignToSpaceSwans(objects, sceneNumber: 1);
        int lastSceneAssigned = ShieldPowerUpPlacementHelpers.AssignToSpaceSwans(objects, sceneNumber: 8);

        Assert.AreEqual(30, firstSceneAssigned);
        Assert.AreEqual(15, lastSceneAssigned);
        Assert.AreEqual(15, objects.Count(o => o.ObjectName == "SpaceSwan" && o.HasPowerUp));
        Assert.IsTrue(objects
            .Where(o => o.ObjectName == "SpaceSwan" && o.HasPowerUp)
            .All(o => o.PowerUpType == PowerUpType.Shield));
    }

    [TestMethod]
    public void ShieldDrop_RequiresSwanWithinFiveHundredUnitsOfShip()
    {
        var shipPosition = new Vector3(1000f, 0f, 1000f);
        var swan = new OmegaObject3D
        {
            ObjectId = 1,
            ObjectName = "SpaceSwan",
            WorldPosition = new Vector3(1499f, 0f, 1000f)
        };
        var drone = new OmegaObject3D
        {
            ObjectId = 2,
            ObjectName = "KamikazeDrone",
            IsActive = true,
            WorldPosition = new Vector3(2000f, 0f, 1000f),
            ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.KamikazeDroneHealth }
        };
        var aiObjects = new List<OmegaObject3D> { drone };

        Assert.IsTrue(ShieldPowerUpDropHelpers.CanDropShield(swan, aiObjects, shipPosition));

        swan.WorldPosition = new Vector3(1501f, 0f, 1000f);
        Assert.IsFalse(ShieldPowerUpDropHelpers.CanDropShield(swan, aiObjects, shipPosition),
            "An off-screen Swan must not leave a distant Shield behind.");
    }

    [TestMethod]
    public void ShieldDrop_RequiresAtLeastOneLiveDroneWithinFiveScreenRadiusInAnyDirection()
    {
        var shipPosition = new Vector3(1000f, 0f, 1000f);
        var swan = new OmegaObject3D
        {
            ObjectId = 1,
            ObjectName = "SpaceSwan",
            WorldPosition = new Vector3(1000f, 0f, 1000f)
        };
        var drone = new OmegaObject3D
        {
            ObjectId = 2,
            ObjectName = "KamikazeDrone",
            IsActive = true,
            ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.KamikazeDroneHealth }
        };
        var aiObjects = new List<OmegaObject3D> { drone };
        float radius = SurfaceSetup.DefaultViewPortSize * SurfaceSetup.tileSize *
            ShieldPowerUpDropHelpers.DroneSearchDistanceInScreens;
        float inside = radius - 1f;
        float diagonal = inside / MathF.Sqrt(2f);

        var positionsInsideRadius = new[]
        {
            new Vector3(shipPosition.x + inside, 0f, shipPosition.z),
            new Vector3(shipPosition.x - inside, 0f, shipPosition.z),
            new Vector3(shipPosition.x, 0f, shipPosition.z + inside),
            new Vector3(shipPosition.x, 0f, shipPosition.z - inside),
            new Vector3(shipPosition.x + diagonal, 0f, shipPosition.z + diagonal)
        };

        foreach (var dronePosition in positionsInsideRadius)
        {
            drone.WorldPosition = dronePosition;
            Assert.IsTrue(ShieldPowerUpDropHelpers.CanDropShield(swan, aiObjects, shipPosition),
                "A live Drone within five screens must enable Shield drops in every direction.");
        }

        drone.WorldPosition = new Vector3(shipPosition.x + radius + 1f, 0f, shipPosition.z);
        Assert.IsFalse(ShieldPowerUpDropHelpers.CanDropShield(swan, aiObjects, shipPosition),
            "A Drone outside the five-screen radius must not enable Shield drops.");

        var secondDrone = new OmegaObject3D
        {
            ObjectId = 3,
            ObjectName = "KamikazeDrone",
            IsActive = true,
            WorldPosition = new Vector3(shipPosition.x, 0f, shipPosition.z - inside),
            ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.KamikazeDroneHealth }
        };
        aiObjects.Add(secondDrone);
        Assert.IsTrue(ShieldPowerUpDropHelpers.CanDropShield(swan, aiObjects, shipPosition),
            "One nearby live Drone is sufficient even when another Drone is outside the radius.");
        aiObjects.Remove(secondDrone);

        drone.IsActive = false;
        drone.WorldPosition = new Vector3(shipPosition.x + inside, 0f, shipPosition.z);
        Assert.IsFalse(ShieldPowerUpDropHelpers.CanDropShield(swan, aiObjects, shipPosition),
            "Inactive drones must not keep Shield drops enabled.");

        drone.IsActive = true;
        drone.ImpactStatus!.ObjectHealth = 0;
        Assert.IsFalse(ShieldPowerUpDropHelpers.CanDropShield(swan, aiObjects, shipPosition),
            "Destroyed drones must not keep Shield drops enabled.");
    }

    [TestMethod]
    public void ShieldPowerUpAndShipGlow_UseDedicatedLowPolyParts()
    {
        var powerUp = PowerUp.CreatePowerup(null!, PowerUpType.Shield);
        var shieldBody = powerUp.ObjectParts.Single(part => part.PartName == "ShieldPowerUpBody");
        var ship = Ship.CreateShip(null!);
        var shieldGlow = ship.ObjectParts.Single(part => part.PartName == "ShieldGlow");

        Assert.AreEqual(28, shieldBody.Triangles.Count);
        Assert.AreEqual(32, shieldGlow.Triangles.Count);
        Assert.IsFalse(shieldGlow.IsVisible);
        Assert.IsTrue(shieldBody.Triangles.All(HasFiniteNonDegenerateGeometry));
        Assert.IsTrue(shieldGlow.Triangles.All(HasFiniteNonDegenerateGeometry));
    }

    private static bool HasFiniteNonDegenerateGeometry(ITriangleMeshWithColorAndTexture triangle)
    {
        bool finite = IsFinite(triangle.vert1) && IsFinite(triangle.vert2) && IsFinite(triangle.vert3);
        if (!finite)
            return false;

        var edge1 = VectorMath.Subtract(triangle.vert2, triangle.vert1);
        var edge2 = VectorMath.Subtract(triangle.vert3, triangle.vert1);
        return VectorMath.Length(MeshGeometryOperations.Cross(edge1, edge2)) > 0.001f;
    }

    private static bool IsFinite(IVector3 vertex) =>
        float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z);
}
