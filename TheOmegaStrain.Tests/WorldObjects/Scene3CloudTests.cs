using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Game.Scenes.Scene3;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Runtime.Loops;
using TheOmegaStrain.Runtime.Rendering;
using TheOmegaStrain.Wpf.Rendering;

namespace TheOmegaStrain.Tests.WorldObjects;

[TestClass]
public class Scene3CloudTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.WeatherVisualState = new WeatherVisualState();
        GameState.ShipState = new ShipState();
        GameState.SettingsState = new GameSettingsState { CloudsEnabled = true };
        GameState.WorldFade = new WorldFadeState();
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void CloudFactory_CreatesIndependentNonCollidingVariants()
    {
        var surface = new Surface();
        var wide = Cloud.CreateCloud(surface, CloudVariant.Wide);
        var tall = Cloud.CreateCloud(surface, CloudVariant.Tall);
        var broken = Cloud.CreateCloud(surface, CloudVariant.Broken);

        Assert.AreEqual(300, wide.ObjectParts.Single(p => p.PartName == CloudVisualSetup.BodyPartName).Triangles.Count);
        Assert.AreEqual(360, tall.ObjectParts.Single(p => p.PartName == CloudVisualSetup.BodyPartName).Triangles.Count);
        Assert.AreEqual(420, broken.ObjectParts.Single(p => p.PartName == CloudVisualSetup.BodyPartName).Triangles.Count);
        foreach (var cloud in new[] { wide, tall, broken })
        {
            Assert.IsTrue(cloud.ObjectParts.Single(p => p.PartName == CloudVisualSetup.BodyPartName).IsVisible);
            Assert.IsTrue(cloud.HasShadow);
            var shadow = cloud.ObjectParts.Single(p => p.PartName == "Shadow");
            Assert.IsFalse(shadow.IsVisible);
            Assert.IsTrue(shadow.Triangles.Count > 0);
            Assert.AreEqual(0, cloud.CrashBoxes.Count);
            Assert.IsNull(cloud.Movement);
            Assert.IsNull(cloud.Particles);
            var vertices = cloud.ObjectParts.Single(p => p.PartName == CloudVisualSetup.BodyPartName).Triangles
                .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 });
            Assert.IsTrue(vertices.Max(v => v.x) - vertices.Min(v => v.x) <= Cloud.MaximumWidth * 0.85f);
        }

        Assert.AreNotSame(wide.ObjectParts[0].Triangles[0].vert1, tall.ObjectParts[0].Triangles[0].vert1);
    }

    [TestMethod]
    public void Cloud_LeavesViewportAtPerspectiveSizeCap()
    {
        var cloud = Cloud.CreateCloud(new Surface(), CloudVariant.Wide);
        cloud.ObjectOffsets = new Vector3(0f, -200f, 400f);
        Assert.IsTrue(ObjectShadowManager.ShouldRenderShadowForCaster(cloud),
            "A visible cloud should keep its shadow.");
        Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(cloud, 0, 0,
            out _, out _, out var initialDepth));

        double capDepth = ScreenSetup.perspectiveAdjustment / SurfaceRenderAnchorHelpers.MaximumPerspectiveScale
            - ScreenSetup.perspectiveAdjustment;
        cloud.ObjectOffsets.z += (float)(capDepth - initialDepth - 1d);

        Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(cloud, 0, 0,
            out _, out _, out var nearDepth));
        Assert.IsTrue(nearDepth < capDepth);
        Assert.IsFalse(OmegaPerspectiveProjectorFactory.IntersectsViewport(cloud));
        Assert.IsFalse(ObjectShadowManager.ShouldRenderShadowForCaster(cloud),
            "Once the renderer culls the cloud at its size cap, it must not leave a shadow.");
    }

    [TestMethod]
    public void OffscreenCaster_DoesNotLeaveShadowInsideViewport()
    {
        var cloud = Cloud.CreateCloud(new Surface(), CloudVariant.Wide);
        cloud.ObjectOffsets = new Vector3(0f, -200f, 400f);
        Assert.IsTrue(ObjectShadowManager.ShouldRenderShadowForCaster(cloud));

        cloud.ObjectOffsets.y = ScreenSetup.screenSizeY * 3f;
        Assert.IsFalse(ObjectShadowManager.ShouldRenderShadowForCaster(cloud));
    }

    [TestMethod]
    public void Scene3_LiveGameLoopProjectsClouds()
    {
        var world = new TestWorld();
        new Scene3().SetupScene(world);
        AddSceneClouds(world, SceneBiomeTypes.Rainforrest);
        var projected = new List<ProjectedTriangleMesh>();
        var crashBoxes = new List<ProjectedTriangleMesh>();

        new LiveGameLoop().UpdateWorld(world, ref projected, ref crashBoxes);

        var cloudTriangles = projected.Where(t => t.PartName == CloudVisualSetup.BodyPartName).ToList();
        Assert.IsTrue(cloudTriangles.Count > 0,
            $"Scene 3 projected {projected.Count} triangles but no clouds.");
        int withinScreen = cloudTriangles.Count(t =>
            Math.Max(t.X1, Math.Max(t.X2, t.X3)) >= 0 &&
            Math.Min(t.X1, Math.Min(t.X2, t.X3)) <= ScreenSetup.screenSizeX &&
            Math.Max(t.Y1, Math.Max(t.Y2, t.Y3)) >= 0 &&
            Math.Min(t.Y1, Math.Min(t.Y2, t.Y3)) <= ScreenSetup.screenSizeY);
        Assert.IsTrue(withinScreen > 0,
            $"Scene 3 has {cloudTriangles.Count} cloud triangles, none inside the viewport. " +
            $"X={cloudTriangles.Min(t => Math.Min(t.X1, Math.Min(t.X2, t.X3)))}.." +
            $"{cloudTriangles.Max(t => Math.Max(t.X1, Math.Max(t.X2, t.X3)))}, " +
            $"Y={cloudTriangles.Min(t => Math.Min(t.Y1, Math.Min(t.Y2, t.Y3)))}.." +
            $"{cloudTriangles.Max(t => Math.Max(t.Y1, Math.Max(t.Y2, t.Y3)))}.");

        GameState.SettingsState.CloudsEnabled = false;
        new LiveGameLoop().UpdateWorld(world, ref projected, ref crashBoxes);
        Assert.IsFalse(projected.Any(t => t.PartName == CloudVisualSetup.BodyPartName),
            "Disabling clouds must omit their geometry from the render set.");
    }

    [TestMethod]
    public void Scene3_SpreadsCloudsAroundPlatformWithoutOverlap()
    {
        var world = new TestWorld();
        new Scene3().SetupScene(world);
        AddSceneClouds(world, SceneBiomeTypes.Rainforrest);
        var clouds = world.WorldInhabitants.OfType<OmegaObject3D>()
            .Where(o => o.ObjectName == "Cloud").ToList();
        var worldClouds = clouds.Where(o => o.WorldPosition.x != 0f || o.WorldPosition.z != 0f).ToList();
        var viewportOrigin = GameState.SurfaceState.GlobalMapPosition;
        float platformOffset = SurfaceSetup.viewPortSize * SurfaceSetup.tileSize / 2f;
        var platformCenter = new Vector3(
            viewportOrigin.x + platformOffset,
            viewportOrigin.y,
            viewportOrigin.z + platformOffset);
        const float spacingX = SceneCloudPool.ForestSpacingXTiles;
        const float spacingZ = SceneCloudPool.ForestSpacingZTiles;
        Assert.AreEqual(100, clouds.Count);
        Assert.AreEqual(100, worldClouds.Count);
        Assert.IsTrue(worldClouds.Select(o => o.ObjectOffsets!.y).Distinct().Count() > 20);
        foreach (var cloud in worldClouds)
        {
            Assert.IsTrue(cloud.ObjectOffsets!.y <= -(220f * 1.21f) * ScreenSetup.ScreenScaleY);
            Assert.IsFalse(GameState.SurfaceState.AiObjects.Contains(cloud));
            Assert.IsInstanceOfType<CloudControls>(cloud.Movement);
            Assert.IsTrue(MathF.Abs(cloud.WorldPosition.x - platformCenter.x) < SurfaceSetup.tileSize * (6f * spacingX));
            Assert.IsTrue(MathF.Abs(cloud.WorldPosition.z - platformCenter.z) < SurfaceSetup.tileSize * (6f * spacingZ));
            Assert.IsTrue(SurfaceSlopeRenderPositionHelpers.ShouldConformToSurfaceSlope(cloud));
            float platformDx = (cloud.WorldPosition.x - platformCenter.x) / SurfaceSetup.tileSize;
            float platformDz = (cloud.WorldPosition.z - platformCenter.z) / SurfaceSetup.tileSize;
            if (cloud.IsActive)
                Assert.IsTrue((platformDx * platformDx + platformDz * platformDz) * SurfaceSetup.tileSize * SurfaceSetup.tileSize >
                    4f * Cloud.MaximumWidth * Cloud.MaximumWidth,
                    "Active clouds must stay clear of the platform center.");
        }
        for (int i = 0; i < worldClouds.Count; i++)
        {
            for (int j = i + 1; j < worldClouds.Count; j++)
            {
                float dx = (worldClouds[i].WorldPosition.x - worldClouds[j].WorldPosition.x) / SurfaceSetup.tileSize;
                float dz = (worldClouds[i].WorldPosition.z - worldClouds[j].WorldPosition.z) / SurfaceSetup.tileSize;
                Assert.IsTrue(MathF.Abs(dx) >= spacingX - 2f || MathF.Abs(dz) >= spacingZ - 2f,
                    "Clouds must maintain their screen-sized grid spacing, allowing for position jitter.");
            }
        }
    }

    [TestMethod]
    public void Scene3_RecyclesOnlyFarEdgeCloudsWhenViewportCrossesOneCell()
    {
        var world = new TestWorld();
        var scene = new Scene3();
        scene.SetupScene(world);
        var pool = AddSceneClouds(world, SceneBiomeTypes.Rainforrest);
        var clouds = world.WorldInhabitants.OfType<OmegaObject3D>()
            .Where(o => o.ObjectName == "Cloud").ToList();
        var original = clouds.Select(o => (o.WorldPosition.x, o.WorldPosition.z)).ToArray();
        int originalObjectCount = world.WorldInhabitants.Count;

        GameState.SurfaceState.GlobalMapPosition.x += SceneCloudPool.ForestSpacingXTiles * SurfaceSetup.tileSize;
        pool.Update(GameState.SurfaceState.GlobalMapPosition);

        Assert.AreEqual(originalObjectCount, world.WorldInhabitants.Count);
        Assert.AreEqual(10, clouds.Where((cloud, index) =>
            cloud.WorldPosition.x != original[index].x || cloud.WorldPosition.z != original[index].z).Count());

        GameState.SurfaceState.GlobalMapPosition.x -= SceneCloudPool.ForestSpacingXTiles * SurfaceSetup.tileSize;
        pool.Update(GameState.SurfaceState.GlobalMapPosition);
        for (int i = 0; i < clouds.Count; i++)
        {
            Assert.AreEqual(original[i].x, clouds[i].WorldPosition.x);
            Assert.AreEqual(original[i].z, clouds[i].WorldPosition.z);
        }
    }

    [TestMethod]
    public void CloudControls_SyncRenderHeightWithoutMovingWorldAnchor()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3(1000f, 0f, 2000f);
        var cloud = Cloud.CreateCloud(new Surface(), CloudVariant.Wide);
        cloud.WorldPosition = new Vector3(1200f, 0f, 2200f);
        cloud.ObjectOffsets = new Vector3(15f, -180f, 400f);
        var controls = new CloudControls();

        controls.MoveObject(cloud, null, null);
        Assert.AreEqual(-180f, cloud.ObjectOffsets.y, 0.01f);

        GameState.SurfaceState.GlobalMapPosition = new Vector3(1000f, 40f, 2000f);
        controls.MoveObject(cloud, null, null);

        Assert.AreEqual(-180f + 40f * SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY,
            cloud.ObjectOffsets.y, 0.01f);
        Assert.AreEqual(15f, cloud.ObjectOffsets.x, 0.01f);
        Assert.AreEqual(400f, cloud.ObjectOffsets.z, 0.01f);
        Assert.AreEqual(1200f, cloud.WorldPosition.x, 0.01f);
        Assert.AreEqual(2200f, cloud.WorldPosition.z, 0.01f);
    }

    [TestMethod]
    public void BiomeClouds_UseCompactWhiteWinterAndFewerStandardDesertClouds()
    {
        var surface = new Surface();
        var winter = Cloud.CreateCloud(surface, CloudVariant.Wide, SceneBiomeTypes.Winter);
        var forest = Cloud.CreateCloud(surface, CloudVariant.Wide, SceneBiomeTypes.Rainforrest);
        var winterVertices = winter.ObjectParts.Single(p => p.PartName == CloudVisualSetup.BodyPartName)
            .Triangles.SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 });
        var forestVertices = forest.ObjectParts.Single(p => p.PartName == CloudVisualSetup.BodyPartName)
            .Triangles.SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 });
        Assert.IsTrue(winterVertices.Max(v => v.x) - winterVertices.Min(v => v.x) <
            forestVertices.Max(v => v.x) - forestVertices.Min(v => v.x));

        var world = new TestWorld();
        var desertPool = AddSceneClouds(world, SceneBiomeTypes.Desert, surface);
        var desertClouds = world.WorldInhabitants.OfType<OmegaObject3D>()
            .Where(o => o.ObjectName == "Cloud").ToList();
        Assert.IsNotNull(desertPool);
        Assert.AreEqual(SceneCloudPool.DesertGridSize * SceneCloudPool.DesertGridSize, desertClouds.Count);
        Assert.AreEqual(36, desertClouds.Count);
        Assert.IsTrue(desertClouds.All(o => o.ObjectParts.Any(p =>
            p.PartName == CloudVisualSetup.BodyPartName)));
        Assert.AreEqual(forest.ObjectParts.Single(p => p.PartName == CloudVisualSetup.BodyPartName).Triangles.Count,
            desertClouds[0].ObjectParts.Single(p => p.PartName == CloudVisualSetup.BodyPartName).Triangles.Count);
        Assert.AreEqual(1f, CloudVisualSetup.GetOpacity(CloudVisualSetup.BodyPartName));
    }

    private static SceneCloudPool AddSceneClouds(TestWorld world, SceneBiomeTypes biome,
        ISurface? surface = null) =>
        SceneCloudPool.AddClouds(world,
            surface ?? GameState.SurfaceState.SurfaceViewportObject?.ParentSurface ?? new Surface(), biome)!;

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = new TestSceneHandler();
        public IGameEventBus? EventBus { get; set; } = new GameEventBus();
        public bool IsPaused { get; set; }
    }

    private sealed class TestSceneHandler : ISceneHandler
    {
        public void SetupActiveScene(I3dWorld world) { }
        public void ResetActiveScene(I3dWorld world) { }
        public void ResetActiveSceneToPlanetStart(I3dWorld world) { }
        public void NextScene(I3dWorld world) { }
        public IScene GetActiveScene() => null!;
        public void HandleKeyPress(GameInputKey key, I3dWorld world) { }
        public void HandleOverlayActivation(I3dWorld world) { }
        public void UpdateFrame(I3dWorld world) { }
    }
}
