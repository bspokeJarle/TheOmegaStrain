using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Collections.Generic;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class TowerControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        ScreenSetup.Initialize(1500, 1024);
    }

    [TestMethod]
    public void MoveObject_AppliesGroundContactNudgeWithoutStacking()
    {
        var control = new TowerControls();
        var tower = CreateTower();

        control.MoveObject(tower, null, null);

        float expectedY = 280f + LandBasedObjectSetup.GroundContactNudgeYScaled;
        Assert.AreEqual(expectedY, tower.ObjectOffsets!.y, 0.001f);

        control.MoveObject(tower, null, null);

        Assert.AreEqual(
            expectedY,
            tower.ObjectOffsets!.y,
            0.001f,
            "Tower ground-contact nudge should be anchored to the original scene offset, not accumulated every frame.");

        control.Dispose();
    }

    private static OmegaObject3D CreateTower()
    {
        return new OmegaObject3D
        {
            ObjectId = 101,
            ObjectName = "Tower",
            ObjectOffsets = new Vector3 { x = 75f, y = 280f, z = 400f },
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>(),
            ImpactStatus = new ImpactStatus()
        };
    }
}
