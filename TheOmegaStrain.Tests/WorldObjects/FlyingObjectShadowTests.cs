using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.WorldObjects;

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
        var objects = new Dictionary<string, _3dObject>
        {
            ["Seeder"] = Seeder.CreateSeeder(null!),
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
}
