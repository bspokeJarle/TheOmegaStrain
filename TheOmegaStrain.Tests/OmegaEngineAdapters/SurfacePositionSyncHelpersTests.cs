using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonSetup;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.OmegaEngineAdapters;

[TestClass]
public class SurfacePositionSyncHelpersTests
{
    [TestInitialize]
    public void Setup()
    {
        ScreenSetup.Initialize(1500, 1024);
    }

    [TestMethod]
    public void GetMinimapMarkerWorldPosition_UsesSurfaceViewportCenterAndObjectOffset()
    {
        var obj = CreateObject(worldX: 250f, worldZ: 2000f, offsetX: 25f);
        int viewportCenterOffset = (SurfaceSetup.viewPortSize * SurfaceSetup.tileSize) / 2;

        var markerWorld = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(obj);

        Assert.IsNotNull(markerWorld);
        Assert.AreEqual(250f + viewportCenterOffset + 25f, markerWorld.x, 0.1f);
        Assert.AreEqual(2000f, markerWorld.z, 0.1f);
    }

    [TestMethod]
    public void GetGuidanceTargetWorldPosition_UsesShipNavigationCenterAndObjectOffset()
    {
        var obj = CreateObject(worldX: 250f, worldZ: 2000f, offsetX: 25f);

        var guidanceWorld = SurfacePositionSyncHelpers.GetGuidanceTargetWorldPosition(obj);

        Assert.IsNotNull(guidanceWorld);
        Assert.AreEqual(250f + ScreenSetup.screenSizeX / 2f + 25f, guidanceWorld.x, 0.1f);
        Assert.AreEqual(2000f, guidanceWorld.z, 0.1f);
    }

    private static _3dObject CreateObject(float worldX, float worldZ, float offsetX)
    {
        return new _3dObject
        {
            ObjectId = 1,
            ObjectName = "Seeder",
            WorldPosition = new Vector3 { x = worldX, y = 0f, z = worldZ },
            ObjectOffsets = new Vector3 { x = offsetX, y = 0f, z = 0f },
            Rotation = new Vector3(),
            ImpactStatus = new ImpactStatus()
        };
    }
}
