using _3dRotations.Helpers;
using Domain;

namespace _3DSpesificsUnitTests.WorldObjects;

[TestClass]
public class FishJumpAreaTests
{
    private const int MaxHeight = 100;

    [TestMethod]
    public void FindFishJumpAreas_ReturnsOneAreaPerLargeWaterComponent()
    {
        var map = CreateLandMap(30);
        FillWater(map, startX: 2, startZ: 2, width: 6, height: 2);
        FillWater(map, startX: 15, startZ: 2, width: 5, height: 2);
        FillWater(map, startX: 8, startZ: 18, width: 8, height: 3);

        var areas = SurfaceGeneration.FindFishJumpAreas(map, MaxHeight, minWidthTiles: 6, minHeightTiles: 2, maxAreas: 100);

        Assert.AreEqual(2, areas.Count);
        Assert.IsTrue(areas.TrueForAll(area => area.WidthTiles >= 6 && area.HeightTiles >= 2));
    }

    [TestMethod]
    public void FindFishJumpAreas_UsesOnlyOneFishPerConnectedWaterBody()
    {
        var map = CreateLandMap(30);
        FillWater(map, startX: 2, startZ: 2, width: 12, height: 4);

        var areas = SurfaceGeneration.FindFishJumpAreas(map, MaxHeight, minWidthTiles: 6, minHeightTiles: 2, maxAreas: 100);

        Assert.AreEqual(1, areas.Count);
        Assert.AreEqual(48, areas[0].ComponentTileCount);
    }

    [TestMethod]
    public void FindFishJumpAreas_RespectsMaxAreas()
    {
        var map = CreateLandMap(30);
        for (int i = 0; i < 4; i++)
            FillWater(map, startX: 2, startZ: 2 + i * 7, width: 6, height: 2);

        var areas = SurfaceGeneration.FindFishJumpAreas(map, MaxHeight, minWidthTiles: 6, minHeightTiles: 2, maxAreas: 3);

        Assert.AreEqual(3, areas.Count);
    }

    [TestMethod]
    public void FindFishJumpAreas_WithPriority_ReturnsClosestWaterBodiesFirst()
    {
        var map = CreateLandMap(30);
        FillWater(map, startX: 2, startZ: 2, width: 6, height: 2);
        FillWater(map, startX: 20, startZ: 20, width: 6, height: 2);

        var areas = SurfaceGeneration.FindFishJumpAreas(
            map,
            MaxHeight,
            minWidthTiles: 6,
            minHeightTiles: 2,
            maxAreas: 1,
            priorityTileX: 23,
            priorityTileZ: 21);

        Assert.AreEqual(1, areas.Count);
        Assert.AreEqual(23, areas[0].CenterTileX);
        Assert.AreEqual(21, areas[0].CenterTileZ);
    }

    [TestMethod]
    public void FindFishJumpAreas_WithPriority_PlacesFishNearPriorityInsideLargeWaterBody()
    {
        var map = CreateLandMap(30);
        FillWater(map, startX: 2, startZ: 2, width: 20, height: 4);

        var areas = SurfaceGeneration.FindFishJumpAreas(
            map,
            MaxHeight,
            minWidthTiles: 6,
            minHeightTiles: 2,
            maxAreas: 100,
            priorityTileX: 20,
            priorityTileZ: 3);

        Assert.AreEqual(1, areas.Count);
        Assert.IsTrue(areas[0].CenterTileX >= 18, "Fish should be anchored near the visible priority area, not at the first rectangle in the water body.");
        Assert.AreEqual(2, areas[0].StartTileX);
        Assert.AreEqual(21, areas[0].EndTileX);
        Assert.AreEqual(20, areas[0].WidthTiles);
    }

    private static SurfaceData[,] CreateLandMap(int size)
    {
        var map = new SurfaceData[size, size];
        int mapId = 0;
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                mapId++;
                map[z, x] = new SurfaceData
                {
                    mapId = mapId,
                    mapDepth = 50,
                    isInfected = false
                };
            }
        }

        return map;
    }

    private static void FillWater(SurfaceData[,] map, int startX, int startZ, int width, int height)
    {
        for (int z = startZ; z < startZ + height; z++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                map[z, x].mapDepth = 0;
                map[z, x].isInfected = false;
                map[z, x].isCratered = false;
                map[z, x].hasLandbasedObject = false;
            }
        }
    }
}
