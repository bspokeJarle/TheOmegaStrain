using _3dRotations.Scene.Scene4;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.Events;
using Domain;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.SceneManagement;

[TestClass]
public class Scene4BearSpawningTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void SetupScene_SpawnsPolarBears_IntoWorldAndAiObjects()
    {
        var scene = new Scene4();
        var world = new TestWorld();

        scene.SetupScene(world);

        var worldBears = world.WorldInhabitants.Where(o => o.ObjectName == "PolarBear").ToList();
        var aiBears = GameState.SurfaceState.AiObjects.Where(o => o.ObjectName == "PolarBear").ToList();
        var snowEmitters = world.WorldInhabitants.Where(o => o.ObjectName == "SnowEmitter").ToList();

        Assert.AreEqual(1, snowEmitters.Count, "Winter scene should add exactly one snow emitter.");
        Assert.AreEqual(0, snowEmitters[0].CrashBoxes.Count, "Snow should not participate in crash detection.");
        Assert.IsNull(snowEmitters[0].Particles, "Snow is rendered by the scene-owned emitter, not a global particle system.");
        Assert.AreEqual(Scene4.TargetPatrolPolarBearCount + 1, worldBears.Count, "Scene4 should spawn one guaranteed bear plus the configured patrol bear target.");
        Assert.AreEqual(worldBears.Count, aiBears.Count, "Spawned polar bears should also be tracked in AiObjects.");
        Assert.AreEqual(worldBears.Count, scene.PolarBearPlacements.Count, "Scene4 should report every spawned polar bear placement.");
        Assert.AreEqual(1, scene.PolarBearPlacements.Count(p => p.Source == "Guaranteed"), "Scene4 should report one guaranteed bear placement.");
        Assert.AreEqual(Scene4.TargetPatrolPolarBearCount, scene.PolarBearPlacements.Count(p => p.Source == "Patrol"), "Scene4 should report all patrol bear placements.");
        Assert.IsTrue(worldBears.All(b => (b.SurfaceBasedId ?? 0) > 0), "Polar bears should be surface-based placements.");
        AssertPolarBearsAreAtLeastOneScreenApart(scene.PolarBearPlacements);

        var map = GameState.SurfaceState.Global2DMap!;
        int sizeX = map.GetLength(1);
        int sizeZ = map.GetLength(0);
        int mapCenterX = sizeX / 2;
        int mapCenterZ = sizeZ / 2;
        int landingAreaSize = 8;
        int landingBufferTiles = 6;
        int landingTopLeftX = System.Math.Max(0, mapCenterX - (landingAreaSize / 2));
        int landingTopLeftZ = System.Math.Max(0, mapCenterZ - (landingAreaSize / 2));
        int landingMinX = landingTopLeftX - landingBufferTiles;
        int landingMinZ = landingTopLeftZ - landingBufferTiles;
        int landingMaxX = landingTopLeftX + landingAreaSize - 1 + landingBufferTiles;
        int landingMaxZ = landingTopLeftZ + landingAreaSize - 1 + landingBufferTiles;

        foreach (var bear in worldBears)
        {
            int mapId = bear.SurfaceBasedId ?? 0;
            bool found = false;
            for (int z = 0; z < sizeZ && !found; z++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    if (map[z, x].mapId != mapId)
                        continue;

                    Assert.IsFalse(
                        x >= landingMinX && x <= landingMaxX && z >= landingMinZ && z <= landingMaxZ,
                        "Polar bears should not spawn on or close to the landing platform area.");
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "Each spawned bear SurfaceBasedId should map to a tile on the surface map.");
        }
    }

    private static void AssertPolarBearsAreAtLeastOneScreenApart(
        IReadOnlyList<Scene4.PolarBearPlacementInfo> placements)
    {
        for (int i = 0; i < placements.Count; i++)
        {
            for (int j = i + 1; j < placements.Count; j++)
            {
                int dx = placements[i].TileX - placements[j].TileX;
                int dz = placements[i].TileZ - placements[j].TileZ;
                double distanceWorld = System.Math.Sqrt((dx * dx) + (dz * dz)) * SurfaceSetup.tileSize;
                Assert.IsTrue(
                    distanceWorld >= ScreenSetup.screenSizeX,
                    $"Polar bears should be at least one screen apart. Pair {i}/{j} was {distanceWorld:0.##} world units.");
            }
        }
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; } = new GameEventBus();
        public bool IsPaused { get; set; }
    }
}
