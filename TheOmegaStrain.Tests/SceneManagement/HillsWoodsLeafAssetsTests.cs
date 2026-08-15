using TheOmegaStrain.Game.Scenes.Scene1;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
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
        Assert.IsTrue(leafTrees.Count > 8000,
            "LeafTree placement should be denser than the original hills/woods pass.");
        Assert.IsTrue(CountLeafTreesNearPlatform(leafTrees, map, searchRadius: 26) >= 8,
            "Some LeafTree objects should be guaranteed near the landing platform.");
        Assert.IsTrue(leafTrees.All(o => !LandingPlatformHelpers.IsSurfaceBasedOnLandingPlatform(map, o.SurfaceBasedId ?? 0)),
            "LeafTree objects should not be placed on the landing platform.");
        Assert.AreEqual(1, world.WorldInhabitants.Count(o => o.ObjectName == "LeafEmitter"),
            "Hills/woods scene should have one leaf emitter.");
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
