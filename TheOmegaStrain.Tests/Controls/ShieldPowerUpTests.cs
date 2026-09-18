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
    public void Shield_ReducesDamageToTwentyPercentForFortyFiveSeconds()
    {
        var gameplay = new GamePlayState();

        gameplay.ActivateShield();

        Assert.AreEqual(GameSetup.ShieldDurationSeconds, gameplay.ShieldSecondsLeft, 0.001f);
        Assert.AreEqual(20, gameplay.CalculateIncomingDamage(100));
        Assert.AreEqual(1f, gameplay.ShieldRemainingFraction, 0.001f);

        gameplay.ApplyDamage(50f, invulnerableSeconds: 0f);
        Assert.AreEqual(90f, gameplay.Health, 0.001f);

        gameplay.Update(44f);
        Assert.AreEqual(20, gameplay.CalculateIncomingDamage(100));
        Assert.AreEqual(1f / 45f, gameplay.ShieldRemainingFraction, 0.001f);

        gameplay.Update(1f);
        Assert.IsFalse(gameplay.IsShieldActive);
        Assert.AreEqual(100, gameplay.CalculateIncomingDamage(100));
    }

    [TestMethod]
    public void SpaceSwans_ShieldCarrierRatioFallsFromHalfToQuarterAcrossCampaign()
    {
        var objects = new List<I3dObject>();
        for (int i = 0; i < 100; i++)
            objects.Add(new OmegaObject3D { ObjectId = i, ObjectName = "SpaceSwan" });

        int firstSceneAssigned = ShieldPowerUpPlacementHelpers.AssignToSpaceSwans(objects, sceneNumber: 1);
        int lastSceneAssigned = ShieldPowerUpPlacementHelpers.AssignToSpaceSwans(objects, sceneNumber: 8);

        Assert.AreEqual(50, firstSceneAssigned);
        Assert.AreEqual(25, lastSceneAssigned);
        Assert.AreEqual(25, objects.Count(o => o.ObjectName == "SpaceSwan" && o.HasPowerUp));
        Assert.IsTrue(objects
            .Where(o => o.ObjectName == "SpaceSwan" && o.HasPowerUp)
            .All(o => o.PowerUpType == PowerUpType.Shield));
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
