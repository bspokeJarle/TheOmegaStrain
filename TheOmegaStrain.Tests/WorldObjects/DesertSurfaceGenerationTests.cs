using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;
using System.Reflection;

namespace _3DSpesificsUnitTests.WorldObjects;

[TestClass]
public class DesertSurfaceGenerationTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState
        {
            SceneBiome = SceneBiomeTypes.Desert
        };
    }

    [TestMethod]
    public void ReturnPseudoRandomMap_ForDesert_CreatesFewLargeLakeComponents()
    {
        var map = SurfaceGeneration.ReturnPseudoRandomMap(MapSetup.globalMapSize, out int maxHeight, 30000, 15000);

        var allComponents = FindDeepWaterComponents(map, maxHeight);
        var components = allComponents
            .Where(c => c.Count >= 100 && c.Width >= 10 && c.Height >= 10)
            .ToList();

        Assert.IsTrue(components.Count >= 3 && components.Count <= 4,
            $"Desert should have 3-4 large lakes, not scattered small ponds. Found {components.Count} large components.");
        Assert.IsFalse(allComponents.Any(c => c.Count < 20),
            "Desert should not have isolated single-tile or tiny water patches.");
        Assert.IsTrue(components.All(c => c.Width >= 10 && c.Height >= 10),
            "Each desert lake should be at least about 10x10 tiles.");
        Assert.IsTrue(components.Any(c => IsNearPlatform(map, c)),
            "One large desert lake should be close enough to the starting platform to become an early oasis landmark.");
    }

    [TestMethod]
    public void FindDesertOasisPlantPlacements_ReturnsDryTilesNearWater()
    {
        var map = SurfaceGeneration.ReturnPseudoRandomMap(MapSetup.globalMapSize, out int maxHeight, 30000, 15000);

        var placements = SurfaceGeneration.FindDesertOasisPlantPlacements(map, MapSetup.globalMapSize, maxHeight, maxPlants: 40);

        Assert.IsTrue(placements.Count > 0);
        Assert.IsTrue(placements.All(p => IsDry(map[p.y, p.x], maxHeight)));
        Assert.IsTrue(placements.All(p => HasWaterNearby(map, maxHeight, p.x, p.y)));
    }

    [TestMethod]
    public void GenerateCrashBoxes_ForDesert_SkipsHighlandsAndKeepsMountains()
    {
        var highlandMap = CreateFlatMap(8, depth: 20);
        FillArea(highlandMap, startX: 2, startY: 2, width: 3, height: 3, depth: 50);

        InvokeGenerateCrashBoxes(highlandMap, maxHeight: 100);

        Assert.IsFalse(HasAnyCrashBox(highlandMap), "Desert surface crashboxes should not be generated for highland dunes.");

        var mountainMap = CreateFlatMap(8, depth: 20);
        FillArea(mountainMap, startX: 2, startY: 2, width: 3, height: 3, depth: 80);

        InvokeGenerateCrashBoxes(mountainMap, maxHeight: 100);

        Assert.IsTrue(HasAnyCrashBox(mountainMap), "Desert surface crashboxes should still be generated for mountain terrain.");
    }

    [TestMethod]
    public void GenerateCrashBoxes_ForNonDesert_StillIncludesHighlands()
    {
        GameState.SurfaceState.SceneBiome = SceneBiomeTypes.HillsWoods;
        var map = CreateFlatMap(8, depth: 20);
        FillArea(map, startX: 2, startY: 2, width: 3, height: 3, depth: 50);

        InvokeGenerateCrashBoxes(map, maxHeight: 100);

        Assert.IsTrue(HasAnyCrashBox(map), "Existing non-desert terrain crashbox behavior should be preserved.");
    }

    [TestMethod]
    public void FindHousePlacementAreas_WithSmallerPlacementSpacing_ReturnsMorePlacements()
    {
        var map = CreateFlatMap(120, depth: 50);

        var sparsePlacements = SurfaceGeneration.FindHousePlacementAreas(
            map,
            mapSize: 120,
            maxHeight: 100,
            existingTrees: new List<(int x, int y, int height)>(),
            overrideMaxHouses: 1000,
            placementSpacing: 40);

        var densePlacements = SurfaceGeneration.FindHousePlacementAreas(
            map,
            mapSize: 120,
            maxHeight: 100,
            existingTrees: new List<(int x, int y, int height)>(),
            overrideMaxHouses: 1000,
            placementSpacing: 20);

        Assert.IsTrue(densePlacements.Count > sparsePlacements.Count,
            $"Smaller placement spacing should increase available placements. Sparse={sparsePlacements.Count}, Dense={densePlacements.Count}.");
    }

    [TestMethod]
    public void GenerateCrashBoxes_SplitsLargeFormationsIntoBoundedBoxes()
    {
        var map = CreateFlatMap(18, depth: 20);
        FillArea(map, startX: 3, startY: 3, width: 12, height: 12, depth: 80);

        InvokeGenerateCrashBoxes(map, maxHeight: 100);

        var crashBoxes = GetCrashBoxes(map);
        Assert.AreEqual(4, crashBoxes.Count, "A large 12x12 formation should be split into four bounded terrain crashboxes.");
        Assert.IsTrue(crashBoxes.All(box => box.width <= 6 && box.height <= 6), "Terrain crashboxes should stay small enough to avoid giant phantom collision volumes.");
    }

    private static List<WaterComponent> FindDeepWaterComponents(SurfaceData[,] map, int maxHeight)
    {
        int height = map.GetLength(0);
        int width = map.GetLength(1);
        var visited = new bool[height, width];
        var components = new List<WaterComponent>();

        for (int y = SurfaceSetup.viewPortSize; y < height - SurfaceSetup.viewPortSize; y++)
        {
            for (int x = SurfaceSetup.viewPortSize; x < width - SurfaceSetup.viewPortSize; x++)
            {
                if (visited[y, x] || !IsDeepWater(map[y, x], maxHeight))
                    continue;

                int count = 0;
                int minX = x;
                int maxX = x;
                int minY = y;
                int maxY = y;
                var queue = new Queue<(int x, int y)>();
                visited[y, x] = true;
                queue.Enqueue((x, y));

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    count++;
                    minX = Math.Min(minX, current.x);
                    maxX = Math.Max(maxX, current.x);
                    minY = Math.Min(minY, current.y);
                    maxY = Math.Max(maxY, current.y);

                    Enqueue(current.x - 1, current.y);
                    Enqueue(current.x + 1, current.y);
                    Enqueue(current.x, current.y - 1);
                    Enqueue(current.x, current.y + 1);
                }

                components.Add(new WaterComponent(count, minX, minY, maxX, maxY));

                void Enqueue(int nx, int ny)
                {
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        return;
                    if (visited[ny, nx] || !IsDeepWater(map[ny, nx], maxHeight))
                        return;

                    visited[ny, nx] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }

        return components;
    }

    private static void InvokeGenerateCrashBoxes(SurfaceData[,] map, int maxHeight)
    {
        var method = typeof(SurfaceGeneration).GetMethod("GenerateCrashBoxes", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(null, new object[] { map, maxHeight });
    }

    private static SurfaceData[,] CreateFlatMap(int size, int depth)
    {
        var map = new SurfaceData[size, size];
        int mapId = 0;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                map[y, x] = new SurfaceData
                {
                    mapDepth = depth,
                    mapId = ++mapId,
                    isInfected = false
                };
            }
        }

        return map;
    }

    private static void FillArea(SurfaceData[,] map, int startX, int startY, int width, int height, int depth)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                var tile = map[y, x];
                tile.mapDepth = depth;
                map[y, x] = tile;
            }
        }
    }

    private static bool HasAnyCrashBox(SurfaceData[,] map)
    {
        return GetCrashBoxes(map).Count > 0;
    }

    private static List<SurfaceData.CrashBoxData> GetCrashBoxes(SurfaceData[,] map)
    {
        var crashBoxes = new List<SurfaceData.CrashBoxData>();

        for (int y = 0; y < map.GetLength(0); y++)
        {
            for (int x = 0; x < map.GetLength(1); x++)
            {
                if (map[y, x].crashBox != null)
                    crashBoxes.Add(map[y, x].crashBox!.Value);
            }
        }

        return crashBoxes;
    }

    private static bool IsNearPlatform(SurfaceData[,] map, WaterComponent component)
    {
        var platformCenter = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);
        int lakeCenterX = (component.MinX + component.MaxX) / 2;
        int lakeCenterY = (component.MinY + component.MaxY) / 2;
        return Math.Abs(lakeCenterX - platformCenter.x) <= 80 &&
               Math.Abs(lakeCenterY - platformCenter.z) <= 80;
    }

    private readonly record struct WaterComponent(int Count, int MinX, int MinY, int MaxX, int MaxY)
    {
        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
    }

    private static bool IsDry(SurfaceData tile, int maxHeight)
    {
        var terrain = CommonUtilities.GamePlayHelpers.GamePlayHelpers.GetTerrainType(tile.mapDepth, maxHeight);
        return terrain == CommonUtilities.GamePlayHelpers.GamePlayHelpers.TerrainType.Grassland ||
               terrain == CommonUtilities.GamePlayHelpers.GamePlayHelpers.TerrainType.Highlands;
    }

    private static bool IsDeepWater(SurfaceData tile, int maxHeight)
    {
        return CommonUtilities.GamePlayHelpers.GamePlayHelpers.GetTerrainType(tile.mapDepth, maxHeight) ==
               CommonUtilities.GamePlayHelpers.GamePlayHelpers.TerrainType.DeepWater;
    }

    private static bool HasWaterNearby(SurfaceData[,] map, int maxHeight, int x, int y)
    {
        for (int oy = -4; oy <= 4; oy++)
        {
            for (int ox = -4; ox <= 4; ox++)
            {
                int nx = x + ox;
                int ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= map.GetLength(1) || ny >= map.GetLength(0))
                    continue;

                var terrain = CommonUtilities.GamePlayHelpers.GamePlayHelpers.GetTerrainType(map[ny, nx].mapDepth, maxHeight);
                if (terrain == CommonUtilities.GamePlayHelpers.GamePlayHelpers.TerrainType.DeepWater ||
                    terrain == CommonUtilities.GamePlayHelpers.GamePlayHelpers.TerrainType.Coast)
                    return true;
            }
        }

        return false;
    }
}
