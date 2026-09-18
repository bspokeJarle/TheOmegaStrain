using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.WorldObjects;

[TestClass]
public class FlyingObjectShadowTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState();
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void GameplayFlyingObjectsOtherThanShip_HaveTerrainShadowParts()
    {
        var objects = new Dictionary<string, OmegaObject3D>
        {
            ["Seeder"] = Seeder.CreateSeeder(null!),
            ["AttackShip"] = AttackShip.CreateAttackShip(null!),
            ["KamikazeDrone"] = KamikazeDrone.CreateKamikazeDrone(null!),
            ["MotherShipSmall"] = MotherShipSmall.CreateMotherShipSmall(null!),
            ["MotherShipMedium"] = MotherShipMedium.CreateMotherShipMedium(null!),
            ["MotherShipLarge"] = MotherShipLarge.CreateMotherShipLarge(null!),
            ["ZeppelinBomber"] = ZeppelinBomber.CreateZeppelinBomber(null!),
            ["BomberBomb"] = BomberBomb.CreateBomberBomb(null!),
            ["SpaceSwan"] = SpaceSwan.CreateSpaceSwan(null!),
            ["DroneDecoy"] = DecoyBeacon.CreateDecoyBeacon(null!),
            ["PowerUp"] = PowerUp.CreatePowerup(null!),
            ["JumpingFish"] = JumpingFish.CreateJumpingFish(null!)
        };

        foreach (var (name, obj) in objects)
        {
            Assert.IsTrue(obj.HasShadow, $"{name} should cast a terrain shadow.");
            Assert.IsTrue(
                obj.ObjectParts.Any(part => part.PartName == "Shadow" && part.Triangles.Count > 0),
                $"{name} should have a prebuilt low-poly Shadow part.");
        }
    }

    [TestMethod]
    public void SpaceSwan_ShadowPreservesWingGapsAndDoesNotShareBodyVertices()
    {
        var swan = SpaceSwan.CreateSpaceSwan(null!);
        var shadow = swan.ObjectParts.Single(p => p.PartName == "Shadow");
        Assert.IsFalse(shadow.IsVisible);
        Assert.IsTrue(shadow.Triangles.Count <= 100, "Keep the cached silhouette inexpensive.");
        var bodyVertices = swan.ObjectParts.Where(p => p.IsVisible)
            .SelectMany(p => p.Triangles).SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 })
            .ToHashSet(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        foreach (var vertex in shadow.Triangles.SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 }))
        {
            Assert.AreEqual(0f, vertex.z, 0.001f);
            Assert.IsFalse(bodyVertices.Contains(vertex), "Shadow placement must not mutate the visible model.");
        }

        Assert.IsTrue(Covers(0f, 0f), "The body must cast a shadow.");
        Assert.IsTrue(Covers(0f, 20f), "The wing must cast a shadow.");
        Assert.IsTrue(Covers(0f, -20f), "Both wings must cast shadows.");
        Assert.IsFalse(Covers(-20f, 20f), "Do not fill the gap between the body and the swept wing.");
        Assert.IsFalse(Covers(-20f, -20f), "Keep the opposite wing gap too.");

        bool Covers(float modelX, float modelY)
        {
            float x = modelX * 1.9f;
            float y = modelY * 1.9f;
            return shadow.Triangles.Any(t => Contains(t, x, y));
        }
        static bool Contains(ITriangleMeshWithColorAndTexture triangle, float x, float y)
        {
            var a = triangle.vert1;
            var b = triangle.vert2;
            var c = triangle.vert3;
            float ab = (b.x - a.x) * (y - a.y) - (b.y - a.y) * (x - a.x);
            float bc = (c.x - b.x) * (y - b.y) - (c.y - b.y) * (x - b.x);
            float ca = (a.x - c.x) * (y - c.y) - (a.y - c.y) * (x - c.x);
            return (ab >= -0.001f && bc >= -0.001f && ca >= -0.001f)
                || (ab <= 0.001f && bc <= 0.001f && ca <= 0.001f);
        }
    }
}
