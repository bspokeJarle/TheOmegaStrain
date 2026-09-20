using TheOmegaStrain.Game.World.Objects;

namespace TheOmegaStrain.Tests.WorldObjects;

[TestClass]
public class ShipParticleGuideTests
{
    [TestMethod]
    public void CreateShip_EngineParticleGuidesAreHiddenAndOutsideTheHull()
    {
        var ship = Ship.CreateShip(null);

        var verticalStart = ship.ObjectParts.Single(p => p.PartName == "JetMotorStartGuide");
        var verticalDirection = ship.ObjectParts.Single(p => p.PartName == "JetMotorDirectionGuide");
        var rearStart = ship.ObjectParts.Single(p => p.PartName == "RearEngineStartGuide");
        var rearDirection = ship.ObjectParts.Single(p => p.PartName == "RearEngineDirectionGuide");

        Assert.IsFalse(verticalStart.IsVisible);
        Assert.IsFalse(verticalDirection.IsVisible);
        Assert.IsFalse(rearStart.IsVisible);
        Assert.IsFalse(rearDirection.IsVisible);

        Assert.IsTrue(verticalStart.Triangles[0].vert1.z < -25f);
        Assert.IsTrue(verticalDirection.Triangles[0].vert1.z < verticalStart.Triangles[0].vert1.z);
        Assert.IsTrue(rearStart.Triangles[0].vert1.y > 70f);
        Assert.IsTrue(rearDirection.Triangles[0].vert1.y > rearStart.Triangles[0].vert1.y);
    }
}
