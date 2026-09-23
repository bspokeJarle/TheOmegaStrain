using TheOmegaStrain.Game.Scenes.Scene1;
using TheOmegaStrain.Game.Scenes.Scene2;
using TheOmegaStrain.Game.Scenes.Scene3;
using TheOmegaStrain.Game.Scenes.Scene4;
using TheOmegaStrain.Game.Scenes.Scene5;
using TheOmegaStrain.Game.Scenes.Scene6;
using TheOmegaStrain.Game.Scenes.Scene7;
using TheOmegaStrain.Game.Scenes.Scene8;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Common.GamePlayHelpers;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class HillsWoodsLeafAssetsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.WeatherVisualState = new WeatherVisualState();
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void Scene1_AddsLeafTreesAndLeafEmitterWithoutReplacingOldTrees()
    {
        var scene = new Scene1();
        var world = new TestWorld();

        scene.SetupScene(world);

        var leafTrees = world.WorldInhabitants.Where(o => o.ObjectName == "LeafTree").ToList();
        var map = GameState.SurfaceState.Global2DMap!;

        Assert.IsTrue(world.WorldInhabitants.Count(o => o.ObjectName == "Tree") > 0,
            "Old Tree objects should still be present.");
        // Tree spacing scales with the surface tile resolution, so the absolute count is
        // much lower than the pre-scaling pass; assert a meaningful coverage floor instead.
        Assert.IsTrue(leafTrees.Count > 500,
            $"LeafTree placement should cover the hills/woods map. Actual: {leafTrees.Count}");
        Assert.IsTrue(CountLeafTreesNearPlatform(leafTrees, map, searchRadius: SurfaceSetup.ScaleTileCount(26)) >= 8,
            "Some LeafTree objects should be guaranteed near the landing platform.");
        Assert.IsTrue(leafTrees.All(o => !LandingPlatformHelpers.IsSurfaceBasedOnLandingPlatform(map, o.SurfaceBasedId ?? 0)),
            "LeafTree objects should not be placed on the landing platform.");
        Assert.AreEqual(1, world.WorldInhabitants.Count(o => o.ObjectName == "LeafEmitter"),
            "Hills/woods scene should have one leaf emitter.");
    }

    [TestMethod]
    public void Scene1_PlacesTreesAndHousesAcrossGrasslandsAndHighlands()
    {
        var scene = new Scene1();
        var world = new TestWorld();

        scene.SetupScene(world);

        const string recording = "Scene1SurfaceRecording_20260830_205856.retro";
        Assert.AreNotEqual(0UL, GameState.SurfaceState.SurfaceHash, "Scene 1 must use its recorded map, not a generated fallback.");
        Assert.IsTrue(TheOmegaStrain.Common.GamePlayHelpers.SurfaceIO.SurfaceIO.TryLoad(
            Path.Combine(AppContext.BaseDirectory, "SceneFiles", recording), out var originalMap, out var originalHash));
        Assert.AreEqual(GameState.SurfaceState.SurfaceHash, originalHash);

        // Scene placement flattens object tiles up to Highlands; classify against the unmodified recording.
        var placedObjects = world.WorldInhabitants
            .Where(o => o.ObjectName is "Tree" or "LeafTree" or "House")
            .Select(o => (o.ObjectName, mapId: o.SurfaceBasedId ?? 0))
            .Where(o => o.mapId > 0)
            .ToList();
        var placedIds = placedObjects.Select(o => o.mapId).ToHashSet();
        var originalTerrainById = new Dictionary<int, GamePlayHelpers.TerrainType>();
        var finalTerrainById = new Dictionary<int, GamePlayHelpers.TerrainType>();
        var finalMap = GameState.SurfaceState.Global2DMap!;
        int grasslandTiles = 0;
        int highlandTiles = 0;
        for (int z = 0; z < originalMap.GetLength(0); z++)
        {
            for (int x = 0; x < originalMap.GetLength(1); x++)
            {
                var tile = originalMap[z, x];
                var terrain = GamePlayHelpers.GetTerrainType(tile.mapDepth, MapSetup.maxHeight);
                if (terrain == GamePlayHelpers.TerrainType.Grassland) grasslandTiles++;
                if (terrain == GamePlayHelpers.TerrainType.Highlands) highlandTiles++;
                if (placedIds.Contains(tile.mapId))
                {
                    originalTerrainById[tile.mapId] = terrain;
                    finalTerrainById[tile.mapId] = GamePlayHelpers.GetTerrainType(
                        finalMap[z, x].mapDepth, MapSetup.maxHeight);
                }
            }
        }

        var placements = placedObjects
            .Where(o => originalTerrainById.ContainsKey(o.mapId))
            .Select(o => (o.ObjectName, terrain: originalTerrainById[o.mapId]))
            .ToList();

        int grasslandTrees = placements.Count(p => (p.ObjectName is "Tree" or "LeafTree") && p.terrain == GamePlayHelpers.TerrainType.Grassland);
        int highlandTrees = placements.Count(p => (p.ObjectName is "Tree" or "LeafTree") && p.terrain == GamePlayHelpers.TerrainType.Highlands);
        int grasslandHouses = placements.Count(p => p.ObjectName == "House" && p.terrain == GamePlayHelpers.TerrainType.Grassland);
        int highlandHouses = placements.Count(p => p.ObjectName == "House" && p.terrain == GamePlayHelpers.TerrainType.Highlands);
        int raisedGrasslandObjects = placedObjects.Count(o =>
            originalTerrainById.GetValueOrDefault(o.mapId) == GamePlayHelpers.TerrainType.Grassland &&
            finalTerrainById.GetValueOrDefault(o.mapId) == GamePlayHelpers.TerrainType.Highlands);

        Console.WriteLine($"Scene 1 original terrain: Grasslands tiles={grasslandTiles}, trees={grasslandTrees}, houses={grasslandHouses}; " +
            $"Highlands tiles={highlandTiles}, trees={highlandTrees}, houses={highlandHouses}; " +
            $"Grassland objects raised to Highlands by placement flattening={raisedGrasslandObjects}.");

        Assert.IsTrue(grasslandTiles > 0 && highlandTiles > 0, "The scene must contain both terrain types.");
        Assert.IsTrue(grasslandTrees > 0 && grasslandHouses > 0, "Grasslands should contain both trees and houses.");
        Assert.IsTrue(highlandTrees > 0, "Highlands should contain trees.");
        Assert.IsTrue(grasslandTrees >= 8000, $"Grasslands should be visibly populated. Actual: {grasslandTrees}.");
        Assert.IsTrue(grasslandHouses >= 300, $"Scene 1 should have more than a handful of houses. Actual: {grasslandHouses}.");
        Assert.IsTrue(raisedGrasslandObjects < grasslandTrees / 10,
            "Decorative footprints should not turn most Grasslands placements into Highlands.");
    }

    [DataTestMethod]
    [DataRow(2, "Scene2SurfaceRecording_20260824_224730.retro", "Tree", "House")]
    [DataRow(3, "Scene3SurfaceRecording_20260823_185258.retro", "LargePalm|SmallPalm|LargeAlienPlant|SmallAlienPlant", "BambooHut")]
    [DataRow(4, "Scene4SurfaceRecording_20260526_193822.retro", "SmallIgloo", "LargeIgloo")]
    [DataRow(5, "Scene5SurfaceRecording_20260526_215801.retro", "LargePalm|SmallPalm|LargeAlienPlant|SmallAlienPlant", "BambooHut")]
    [DataRow(6, "Scene6SurfaceRecording_20260530_desert_lakes.retro", "DesertRockFormation|Cactus|LargeAlienPlant|SmallAlienPlant", "BedouinTent")]
    [DataRow(7, "Scene7SurfaceRecording_20260526_222053.retro", "SmallIgloo", "LargeIgloo")]
    [DataRow(8, "Scene8SurfaceRecording_20260526_223403.retro", "LargePalm|SmallPalm|LargeAlienPlant|SmallAlienPlant", "BambooHut")]
    public void OtherRecordedScenes_PlaceGroundObjectsOnTheirOwnMaps(
        int sceneNumber, string recording, string natureNames, string dwellingNames)
    {
        var world = new TestWorld();
        switch (sceneNumber)
        {
            case 2: new Scene2().SetupScene(world); break;
            case 3: new Scene3().SetupScene(world); break;
            case 4: new Scene4().SetupScene(world); break;
            case 5: new Scene5().SetupScene(world); break;
            case 6: new Scene6().SetupScene(world); break;
            case 7: new Scene7().SetupScene(world); break;
            case 8: new Scene8().SetupScene(world); break;
            default: Assert.Fail($"Unexpected scene: {sceneNumber}"); break;
        }

        Assert.AreNotEqual(0UL, GameState.SurfaceState.SurfaceHash,
            $"Scene {sceneNumber} must load its recorded map.");
        Assert.IsTrue(TheOmegaStrain.Common.GamePlayHelpers.SurfaceIO.SurfaceIO.TryLoad(
            Path.Combine(AppContext.BaseDirectory, "SceneFiles", recording), out var originalMap, out var originalHash),
            $"Missing recording for scene {sceneNumber}: {recording}");
        Assert.AreEqual(originalHash, GameState.SurfaceState.SurfaceHash,
            $"Scene {sceneNumber} loaded a different map than its own recording.");

        var nature = natureNames.Split('|');
        var dwellings = dwellingNames.Split('|');
        var placements = world.WorldInhabitants
            .Where(o => nature.Contains(o.ObjectName) || dwellings.Contains(o.ObjectName))
            .ToList();
        var placedIds = placements.Select(o => o.SurfaceBasedId ?? 0).ToHashSet();
        var originalTerrainById = new Dictionary<int, GamePlayHelpers.TerrainType>();
        var desertObjectsPerViewport = new Dictionary<(int x, int z), int>();
        var desertObjectsById = sceneNumber == 6
            ? placements.GroupBy(o => o.SurfaceBasedId ?? 0).ToDictionary(g => g.Key, g => g.Count())
            : null;
        for (int z = 0; z < originalMap.GetLength(0); z++)
        {
            for (int x = 0; x < originalMap.GetLength(1); x++)
            {
                var tile = originalMap[z, x];
                if (placedIds.Contains(tile.mapId))
                {
                    originalTerrainById[tile.mapId] = GamePlayHelpers.GetTerrainType(tile.mapDepth, MapSetup.maxHeight);
                    if (desertObjectsById?.TryGetValue(tile.mapId, out int count) == true)
                    {
                        var viewport = (x / SurfaceSetup.DefaultViewPortSize, z / SurfaceSetup.DefaultViewPortSize);
                        desertObjectsPerViewport[viewport] = desertObjectsPerViewport.GetValueOrDefault(viewport) + count;
                    }
                }
            }
        }

        int natureOnGrasslands = placements.Count(o => nature.Contains(o.ObjectName) &&
            originalTerrainById.GetValueOrDefault(o.SurfaceBasedId ?? 0) == GamePlayHelpers.TerrainType.Grassland);
        int natureOnHighlands = placements.Count(o => nature.Contains(o.ObjectName) &&
            originalTerrainById.GetValueOrDefault(o.SurfaceBasedId ?? 0) == GamePlayHelpers.TerrainType.Highlands);
        int dwellingsOnGrasslands = placements.Count(o => dwellings.Contains(o.ObjectName) &&
            originalTerrainById.GetValueOrDefault(o.SurfaceBasedId ?? 0) == GamePlayHelpers.TerrainType.Grassland);
        int dwellingsOnHighlands = placements.Count(o => dwellings.Contains(o.ObjectName) &&
            originalTerrainById.GetValueOrDefault(o.SurfaceBasedId ?? 0) == GamePlayHelpers.TerrainType.Highlands);

        Console.WriteLine($"Scene {sceneNumber}: nature={placements.Count(o => nature.Contains(o.ObjectName))} " +
            $"(grasslands={natureOnGrasslands}, highlands={natureOnHighlands}); " +
            $"dwellings={placements.Count(o => dwellings.Contains(o.ObjectName))} " +
            $"(grasslands={dwellingsOnGrasslands}, highlands={dwellingsOnHighlands}).");
        if (sceneNumber == 6)
        {
            int rocks = placements.Count(o => o.ObjectName == "DesertRockFormation");
            int cacti = placements.Count(o => o.ObjectName == "Cactus");
            int tents = placements.Count(o => o.ObjectName == "BedouinTent");
            int towers = world.WorldInhabitants.Count(o => o.ObjectName == "Tower" && o.SurfaceBasedId > 0);
            int busiestViewport = desertObjectsPerViewport.Values.DefaultIfEmpty().Max();
            Console.WriteLine("Scene 6 object counts: " + string.Join(", ", placements
                .GroupBy(o => o.ObjectName)
                .OrderBy(g => g.Key)
                .Select(g => $"{g.Key}={g.Count()}")) + $", Tower={towers}");
            Console.WriteLine($"Scene 6 busiest {SurfaceSetup.DefaultViewPortSize}x{SurfaceSetup.DefaultViewPortSize} tile area: " +
                $"{busiestViewport} ground objects (aligned tile buckets, not a camera render count).");

            Assert.IsTrue(rocks <= 15000 && cacti <= 20000 && tents <= 1500,
                $"Scene 6 decoration budget exceeded: rocks={rocks}, cacti={cacti}, tents={tents}.");
            Assert.IsTrue(placements.Count + towers <= 35000,
                $"Scene 6 has too many total ground decorations: {placements.Count + towers}.");
            Assert.IsTrue(busiestViewport <= 40,
                $"Scene 6 has a very dense ground-object cluster: {busiestViewport} in one tile bucket.");
        }

        Assert.IsTrue(placements.Any(o => nature.Contains(o.ObjectName)),
            $"Scene {sceneNumber} should place its nature objects.");
        Assert.IsTrue(placements.Any(o => dwellings.Contains(o.ObjectName)),
            $"Scene {sceneNumber} should place its dwellings.");
        Assert.IsTrue(placements.All(o => o.SurfaceBasedId > 0 && originalTerrainById.ContainsKey(o.SurfaceBasedId.Value)),
            $"Scene {sceneNumber} should anchor every ground object to a tile in its recording.");
        Assert.IsTrue(placements.All(o => !LandingPlatformHelpers.IsSurfaceBasedOnLandingPlatform(
            GameState.SurfaceState.Global2DMap!, o.SurfaceBasedId!.Value)),
            $"Scene {sceneNumber} should keep nature and dwellings off the landing platform.");
        if (sceneNumber is 2 or 3 or 5 or 8)
            Assert.IsTrue(natureOnGrasslands > 0 && dwellingsOnGrasslands > 0,
                $"Scene {sceneNumber} should populate its original Grasslands.");
    }

    private static int CountLeafTreesNearPlatform(List<I3dObject> leafTrees, SurfaceData[,] map, int searchRadius)
    {
        var lookup = CreateMapIdLookup(map);
        var platformCenter = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);
        int count = 0;

        foreach (var leafTree in leafTrees)
        {
            int mapId = leafTree.SurfaceBasedId ?? 0;
            if (!lookup.TryGetValue(mapId, out var tile))
                continue;

            int dx = tile.x - platformCenter.x;
            int dz = tile.z - platformCenter.z;
            double distance = Math.Sqrt((dx * dx) + (dz * dz));
            if (distance <= searchRadius &&
                !LandingPlatformHelpers.IsLandingPlatformTile(map, tile.x, tile.z))
            {
                count++;
            }
        }

        return count;
    }

    private static Dictionary<int, (int x, int z)> CreateMapIdLookup(SurfaceData[,] map)
    {
        var lookup = new Dictionary<int, (int x, int z)>();
        for (int z = 0; z < map.GetLength(0); z++)
        {
            for (int x = 0; x < map.GetLength(1); x++)
            {
                int mapId = map[z, x].mapId;
                if (mapId > 0)
                    lookup[mapId] = (x, z);
            }
        }

        return lookup;
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; } = new GameEventBus();
        public bool IsPaused { get; set; }
    }
}
