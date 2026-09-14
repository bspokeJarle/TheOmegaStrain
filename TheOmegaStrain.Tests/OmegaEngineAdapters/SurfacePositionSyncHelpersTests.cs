using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.OmegaEngineAdapters;

[TestClass]
public class SurfacePositionSyncHelpersTests
{
    [TestInitialize]
    public void Setup()
    {
        ScreenSetup.Initialize(1500, 1024);
        GameState.ShipState = new TheOmegaStrain.Common.CommonGlobalState.States.ShipState
        {
            ShipObjectOffsets = new Vector3()
        };
    }

    [DataTestMethod]
    [DataRow("AttackShip")]
    [DataRow("AttackShip2")]
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
    [DataRow("AttackShip2")]
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

    [DataTestMethod]
    [DataRow(56f)]
    [DataRow(63f)]
    [DataRow(70f)]
    public void GetSurfacePitchHeightCorrectionY_MatchesDifferenceFromOriginalCalibration(float pitchDegrees)
    {
        const float surfaceLongitudinalDistance = -400f;
        float radians = pitchDegrees * (MathF.PI / 180f);
        float originalRadians = OmegaWorldViewSetup.OriginalWorldPitchDegrees * (MathF.PI / 180f);
        float expectedY = surfaceLongitudinalDistance * (MathF.Cos(radians) - MathF.Cos(originalRadians));
        if (pitchDegrees <= 56.5f)
            expectedY *= SurfacePositionSyncHelpers.LowAngleBackgroundCorrectionFactor;
        else
            expectedY += surfaceLongitudinalDistance *
                         SurfacePositionSyncHelpers.NormalAndHighBackgroundLiftPerWorldUnit;

        float correction = SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(surfaceLongitudinalDistance, pitchDegrees);

        Assert.AreEqual(expectedY, correction, 0.001f);
    }

    [DataTestMethod]
    [DataRow(56f)]
    [DataRow(63f)]
    [DataRow(70f)]
    public void GetSurfacePitchHeightCorrectionY_ForegroundIsUnchanged(float pitchDegrees)
    {
        const float surfaceLongitudinalDistance = 400f;
        float correction = SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(
            surfaceLongitudinalDistance,
            pitchDegrees);

        Assert.AreEqual(0f, correction, 0.001f);
    }

    [TestMethod]
    public void GetSurfacePitchHeightCorrectionY_OriginalAngleGetsSmallBackgroundLift()
    {
        float correction = SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(
            -1500f,
            OmegaWorldViewSetup.OriginalWorldPitchDegrees);

        Assert.AreEqual(
            -1500f * SurfacePositionSyncHelpers.NormalAndHighBackgroundLiftPerWorldUnit,
            correction,
            0.001f);
    }

    [DataTestMethod]
    [DataRow(56f)]
    [DataRow(63f)]
    [DataRow(70f)]
    public void GetSurfacePitchHeightCorrectionY_IsZeroFromCentreForward(float pitchDegrees)
    {
        const float localDistance = 300f;

        float centre = SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(0f, pitchDegrees);
        float back = SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(-localDistance, pitchDegrees);
        float front = SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(localDistance, pitchDegrees);

        Assert.AreEqual(0f, centre, 0.001f);
        Assert.AreEqual(0f, front, 0.001f);
        Assert.AreNotEqual(0f, back, 0.001f);
    }

    [TestMethod]
    public void GetSurfacePitchHeightCorrectionY_ForObject_UsesWorldDistanceAndIgnoresVisualZOffsets()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { z = 1000f };
        GameState.SurfaceState.SurfaceViewportObject = new OmegaObject3D
        {
            ObjectId = 2,
            ObjectOffsets = new Vector3 { z = 25f }
        };
        var obj = CreateObject(worldX: 0f, worldZ: 1300f, offsetX: 0f);
        obj.ObjectOffsets.z = 15f;

        float correction = SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(obj, 63f);

        float expectedSurfaceLongitudinalDistance = 1300f - 1000f;
        Assert.IsTrue(expectedSurfaceLongitudinalDistance > 0f);
        Assert.AreEqual(0f, correction, 0.001f);
    }

    [TestMethod]
    public void AddSurfacePitchHeightCorrectionY_AddsToExistingYOnly()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { z = 1000f };
        GameState.SurfaceState.SurfaceViewportObject = new OmegaObject3D
        {
            ObjectId = 2,
            ObjectOffsets = new Vector3()
        };
        var obj = CreateObject(worldX: 0f, worldZ: 700f, offsetX: 25f);
        obj.IsOnScreen = true;
        obj.ObjectOffsets.y = 125f;
        obj.ObjectOffsets.z = 15f;
        float expectedCorrection = SurfacePositionSyncHelpers.GetSurfacePitchHeightCorrectionY(obj, 63f);

        SurfacePositionSyncHelpers.AddSurfacePitchHeightCorrectionY(obj, 63f);

        Assert.AreEqual(25f, obj.ObjectOffsets.x, 0.001f);
        Assert.AreEqual(125f + expectedCorrection, obj.ObjectOffsets.y, 0.001f);
        Assert.AreEqual(15f, obj.ObjectOffsets.z, 0.001f);
        Assert.AreEqual(700f, obj.WorldPosition.z, 0.001f);
    }

    [TestMethod]
    public void AddSurfacePitchHeightCorrectionY_WhenObjectIsOffScreen_DoesNotChangeOffsets()
    {
        var obj = CreateObject(worldX: 0f, worldZ: 700f, offsetX: 25f);
        obj.IsOnScreen = false;
        obj.ObjectOffsets.y = 125f;

        SurfacePositionSyncHelpers.AddSurfacePitchHeightCorrectionY(obj, 63f);

        Assert.AreEqual(125f, obj.ObjectOffsets.y, 0.001f);
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
