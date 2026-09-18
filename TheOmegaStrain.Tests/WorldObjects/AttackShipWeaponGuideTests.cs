using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.World.Objects;

namespace TheOmegaStrain.Tests.WorldObjects;

[TestClass]
public class AttackShipWeaponGuideTests
{
    [DataTestMethod]
    [DataRow(0.5f)]
    [DataRow(1f)]
    [DataRow(2f)]
    public void WeaponGuides_StartAtNoseWithShipGuideSpacing(float scale)
    {
        var ship = AttackShip.CreateAttackShip(null!);
        OmegaObject3DHelpers.ApplyScaleToObject(ship, scale);

        var startPart = ship.ObjectParts.Single(p => p.PartName == "WeaponStartGuide");
        var directionPart = ship.ObjectParts.Single(p => p.PartName == "WeaponDirectionGuide");
        // Hidden guides must still work as weapon anchors in the release build.
        Assert.IsFalse(startPart.IsVisible);
        Assert.IsFalse(directionPart.IsVisible);
        Assert.AreEqual(1, startPart.Triangles.Count);
        Assert.AreEqual(1, directionPart.Triangles.Count);

        var start = startPart.Triangles[0];
        var direction = directionPart.Triangles[0];
        Assert.IsTrue(start.noHidden == true && direction.noHidden == true);
        Assert.AreEqual("00ff00", start.Color);
        Assert.AreEqual("ff0000", direction.Color);

        var noseTip = ship.ObjectParts.Single(p => p.PartName == "AttackShipNose")
            .Triangles.SelectMany(Vertices).MaxBy(v => v.x)!;
        Assert.AreEqual(noseTip.x, start.vert1.x, 0.001f);
        Assert.AreEqual(noseTip.y, start.vert1.y, 0.001f);
        Assert.AreEqual(noseTip.z, start.vert1.z, 0.001f);

        float shipGuideDistance = Ship.CannonStartGuide()![0].vert1.y
            - Ship.CannonDirectionGuide()![0].vert1.y;
        Assert.AreEqual(240f, shipGuideDistance, 0.001f);
        Assert.AreEqual(48f * 1.5f * scale, noseTip.x, 0.001f);
        Assert.AreEqual(shipGuideDistance * 1.5f * scale, direction.vert1.x - start.vert1.x, 0.001f);
        Assert.AreEqual(start.vert1.y, direction.vert1.y, 0.001f);
        Assert.AreEqual(start.vert1.z, direction.vert1.z, 0.001f);
    }

    [TestMethod]
    public void HiddenWeaponGuides_DoNotChangeGeneratedHullShadow()
    {
        var ship = AttackShip.CreateAttackShip(null!);
        var actualShadow = ship.ObjectParts.Single(p => p.PartName == "Shadow");

        ship.ObjectParts.RemoveAll(p => p.PartName is "WeaponStartGuide" or "WeaponDirectionGuide" or "Shadow");
        OmegaObject3DHelpers.AddSimplifiedShadowPart(ship, useFlatQuad: true);
        var hullOnlyShadow = ship.ObjectParts.Single(p => p.PartName == "Shadow");

        CollectionAssert.AreEqual(
            hullOnlyShadow.Triangles.SelectMany(Vertices).Select(v => (v.x, v.y, v.z)).ToArray(),
            actualShadow.Triangles.SelectMany(Vertices).Select(v => (v.x, v.y, v.z)).ToArray());
    }

    private static IEnumerable<IVector3> Vertices(ITriangleMeshWithColorAndTexture triangle)
        => new[] { triangle.vert1, triangle.vert2, triangle.vert3 };
}
