using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class ResolutionScaledVisibilityTests
{
    [TestCleanup]
    public void Cleanup()
    {
        ScreenSetup.Initialize(1500, 1024);
    }

    [DataTestMethod]
    [DataRow(1500, 1024)]
    [DataRow(2560, 1440)]
    public void DeployedDecoy_RemainsVisibleAtEquivalentViewportPosition(int width, int height)
    {
        ScreenSetup.Initialize(width, height);
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3 { x = 50000f, z = 50000f }
        };

        float viewportCenter = (SurfaceSetup.viewPortSize * SurfaceSetup.tileSize) / 2f;
        float scaledShipOffset = 400f * ScreenSetup.ScreenScaleX;
        var decoy = new OmegaObject3D
        {
            ObjectId = 1,
            ObjectName = "DroneDecoy",
            WorldPosition = new Vector3
            {
                x = GameState.SurfaceState.GlobalMapPosition.x + viewportCenter + scaledShipOffset,
                z = GameState.SurfaceState.GlobalMapPosition.z + viewportCenter
            }
        };

        Assert.IsTrue(decoy.CheckInhabitantVisibility(),
            $"Equivalent Decoy placement should remain visible at {width}x{height}.");
    }
}
