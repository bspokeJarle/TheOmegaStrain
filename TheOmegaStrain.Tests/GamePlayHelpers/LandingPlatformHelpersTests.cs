using TheOmegaStrain.Common.GamePlayHelpers;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.GamePlayHelperTests;

[TestClass]
public class LandingPlatformHelpersTests
{
    [TestMethod]
    public void GetLandingPlatformRect_ReturnsEightTilesAtMapCenter()
    {
        var map = CreateMap(20, 20);

        var rect = LandingPlatformHelpers.GetLandingPlatformRect(map);

        Assert.AreEqual(6, rect.MinX);
        Assert.AreEqual(6, rect.MinZ);
        Assert.AreEqual(13, rect.MaxX);
        Assert.AreEqual(13, rect.MaxZ);
        Assert.AreEqual(LandingPlatformHelpers.LandingPlatformSizeTiles, rect.MaxX - rect.MinX + 1);
        Assert.AreEqual(LandingPlatformHelpers.LandingPlatformSizeTiles, rect.MaxZ - rect.MinZ + 1);
    }

    [TestMethod]
    public void IsLandingPlatformTile_UsesCenteredPlatformRect()
    {
        var map = CreateMap(20, 20);

        Assert.IsTrue(LandingPlatformHelpers.IsLandingPlatformTile(map, 6, 6));
        Assert.IsTrue(LandingPlatformHelpers.IsLandingPlatformTile(map, 13, 13));
        Assert.IsFalse(LandingPlatformHelpers.IsLandingPlatformTile(map, 5, 6));
        Assert.IsFalse(LandingPlatformHelpers.IsLandingPlatformTile(map, 14, 13));
    }

    [TestMethod]
    public void GetLandingPlatformRect_AppliesBufferAndClampsToMapBounds()
    {
        var map = CreateMap(10, 10);

        var rect = LandingPlatformHelpers.GetLandingPlatformRect(map, bufferTiles: 5);

        Assert.AreEqual(0, rect.MinX);
        Assert.AreEqual(0, rect.MinZ);
        Assert.AreEqual(9, rect.MaxX);
        Assert.AreEqual(9, rect.MaxZ);
    }

    [TestMethod]
    public void GetLandingPlatformCenterTile_ReturnsCenterOfPlatformRect()
    {
        var map = CreateMap(20, 20);

        var center = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);

        Assert.AreEqual(10, center.x);
        Assert.AreEqual(10, center.z);
    }

    [TestMethod]
    public void IsSurfaceBasedOnLandingPlatform_ChecksOnlyPlatformMapIds()
    {
        var map = CreateMap(20, 20);

        Assert.IsTrue(LandingPlatformHelpers.IsSurfaceBasedOnLandingPlatform(map, map[6, 6].mapId));
        Assert.IsFalse(LandingPlatformHelpers.IsSurfaceBasedOnLandingPlatform(map, map[5, 6].mapId));
    }

    private static SurfaceData[,] CreateMap(int width, int height)
    {
        var map = new SurfaceData[height, width];
        int mapId = 0;
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                map[z, x] = new SurfaceData
                {
                    mapId = ++mapId,
                    mapDepth = 50,
                    isInfected = false
                };
            }
        }

        return map;
    }
}
