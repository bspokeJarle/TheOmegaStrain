using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.OmegaEngineAdapters;

[TestClass]
[DoNotParallelize]
public class SurfacePositionSyncHelpersTests
{
    private ShipState _originalShip = null!;
    private SurfaceState _originalSurface = null!;
    private int _originalWidth;
    private int _originalHeight;

    [TestInitialize]
    public void Setup()
    {
        _originalShip = GameState.ShipState;
        _originalSurface = GameState.SurfaceState;
        _originalWidth = ScreenSetup.screenSizeX;
        _originalHeight = ScreenSetup.screenSizeY;
        ScreenSetup.Initialize(1500, 1024);
        GameState.ShipState = new ShipState
        {
            ShipObjectOffsets = new Vector3()
        };
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.ShipState = _originalShip;
        GameState.SurfaceState = _originalSurface;
        ScreenSetup.Initialize(_originalWidth, _originalHeight);
    }

    [DataTestMethod]
    [DataRow("AttackShip")]
    [DataRow("CustomEnemy")]
    public void AttackShip_UsesRenderedDepthRatherThanLegacyPursuitMapping(string name)
    {
        var oldSurface = GameState.SurfaceState;
        var oldShip = GameState.ShipState;
        try
        {
            GameState.SurfaceState = new SurfaceState { GlobalMapPosition = new Vector3(50000f, 0f, 51000f) };
            GameState.ShipState = new TheOmegaStrain.Common.CommonGlobalState.States.ShipState
            {
                ShipObjectOffsets = new Vector3(),
                ShipWorldPosition = new Vector3(50750f, 0f, 51512f),
                ShipCrashCenterWorldPosition = new Vector3(50750f, 0f, 51512f)
            };
            var obj = new OmegaObject3D
            {
                ObjectId = 997, ObjectName = name,
                WorldPosition = new Vector3(50000f, 0f, 50900f),
                ObjectOffsets = new Vector3(0f, 0f, 100f)
            };
            var marker = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(obj)!;
            Assert.AreEqual(50000f + MapSetup.viewPortCenterOffsetX, marker.x, 0.01f);
            Assert.AreEqual(50800f + MapSetup.viewPortCenterOffsetX, marker.z, 0.01f,
                "Legacy pursuit reported zero distance here, but rendered depth differs by 200 units.");
        }
        finally
        {
            GameState.SurfaceState = oldSurface;
            GameState.ShipState = oldShip;
        }
    }

    [DataTestMethod]
    [DataRow("KamikazeDrone")]
    [DataRow("AttackShip")]
    [DataRow("CustomEnemy")]
    [DataRow("Seeder")]
    [DataRow("SpaceSwan")]
    [DataRow("ZeppelinBomber")]
    [DataRow("MotherShipLarge")]
    [DataRow("UnknownObject")]
    public void OverlappingRenderedCentres_MatchShipMapMarkerIncludingDepthOffset(string objectName)
    {
        var oldShip = GameState.ShipState;
        var oldSurface = GameState.SurfaceState;
        try
        {
            GameState.SurfaceState = new SurfaceState { GlobalMapPosition = new Vector3(50000f, 0f, 51000f) };
            GameState.ShipState = new TheOmegaStrain.Common.CommonGlobalState.States.ShipState
            {
                ShipObjectOffsets = new Vector3(20f, 0f, 40f),
                ShipWorldPosition = new Vector3(50750f, 0f, 51512f),
                ShipCrashCenterWorldPosition = new Vector3(50750f, 0f, 51512f)
            };
            var obj = new OmegaObject3D
            {
                ObjectId = 999, ObjectName = objectName,
                WorldPosition = new Vector3(50020f, 0f, 51060f),
                ObjectOffsets = new Vector3(0f, 0f, 100f)
            };
            var marker = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(obj)!;
            Assert.AreEqual(50000f + MapSetup.viewPortCenterOffsetX, marker.x, 0.01f);
            Assert.AreEqual(51000f + MapSetup.viewPortCenterOffsetX, marker.z, 0.01f);
        }
        finally
        {
            GameState.ShipState = oldShip;
            GameState.SurfaceState = oldSurface;
        }
    }

    [TestMethod]
    public void MinimapMarker_RotatedCentreAndDepthOffsetFollowRenderedPosition()
    {
        var obj = new OmegaObject3D
        {
            ObjectId = 998, WorldPosition = new Vector3(1000f, 0f, 2000f),
            ObjectOffsets = new Vector3(25f, 0f, 100f),
            Rotation = new Vector3(0f, 180f, 0f),
            CrashBoxes = new List<List<IVector3>>
            {
                new() { new Vector3(0f, 0f, 0f), new Vector3(20f, 0f, 40f) }
            }
        };
        var marker = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(obj)!;
        Assert.AreEqual(1000f + MapSetup.viewPortCenterOffsetX + 15f, marker.x, 0.01f);
        Assert.AreEqual(2000f + MapSetup.viewPortCenterOffsetX - 80f, marker.z, 0.01f);
    }

    [TestMethod]
    public void GetMinimapMarkerWorldPosition_UsesSurfaceViewportCenterAndObjectOffset()
    {
        var obj = CreateObject(worldX: 250f, worldZ: 2000f, offsetX: 25f);
        int viewportCenterOffset = (SurfaceSetup.viewPortSize * SurfaceSetup.tileSize) / 2;

        var markerWorld = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(obj);

        Assert.IsNotNull(markerWorld);
        Assert.AreEqual(250f + viewportCenterOffset + 25f, markerWorld.x, 0.1f);
        Assert.AreEqual(2000f + viewportCenterOffset, markerWorld.z, 0.1f);
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

    private static OmegaObject3D CreateObject(float worldX, float worldZ, float offsetX)
    {
        return new OmegaObject3D
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
