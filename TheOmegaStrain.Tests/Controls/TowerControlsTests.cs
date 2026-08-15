using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

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

    private static _3dObject CreateTower()
    {
        return new _3dObject
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
