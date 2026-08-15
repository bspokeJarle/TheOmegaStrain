using CommonUtilities.GamePlayHelpers;

namespace _3DSpesificsUnitTests.CommonHelpers;

[TestClass]
public class MapCoordinateHelpersTests
{
    [TestMethod]
    public void WorldToTileIndex_ReturnsContainingTile_NotNearestTileCenter()
    {
        int tile = MapCoordinateHelpers.WorldToTileIndex(2.75f * 75f, 75, 10);

        Assert.AreEqual(2, tile);
    }

    [TestMethod]
    public void WorldToTileIndex_WrapsNegativeAndOverflowCoordinates()
    {
        Assert.AreEqual(9, MapCoordinateHelpers.WorldToTileIndex(-1f, 75, 10));
        Assert.AreEqual(0, MapCoordinateHelpers.WorldToTileIndex(10f * 75f, 75, 10));
    }

    [TestMethod]
    public void GetWrappedRelativeIndex_ReturnsPositionInsideWrappedCrop()
    {
        Assert.AreEqual(2, MapCoordinateHelpers.GetWrappedRelativeIndex(1, -1, 10));
        Assert.AreEqual(1, MapCoordinateHelpers.GetWrappedRelativeIndex(0, 9, 10));
    }
}
