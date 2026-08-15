using TheOmegaStrain.Game.Scenes.Scene8;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Domain;
using System.Linq;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class Scene8RainforestAssetTests
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
    public void SetupScene_UsesRainforestBiomeWeatherAndSurfaceAssets()
    {
        var scene = new Scene8();
        var world = new TestWorld();

        scene.SetupScene(world);

        Assert.AreEqual(SceneBiomeTypes.Rainforrest, scene.SceneBiome);
        Assert.AreEqual(1, world.WorldInhabitants.Count(o => o.ObjectName == "RainEmitter"));
        Assert.AreEqual(1, world.WorldInhabitants.Count(o => o.ObjectName == "LightningEmitter"));
        Assert.AreEqual(0, world.WorldInhabitants.Count(o => o.ObjectName == "SnowEmitter"));

        Assert.IsTrue(world.WorldInhabitants.Any(o =>
                o.ObjectName == "LargePalm" ||
                o.ObjectName == "SmallPalm" ||
                o.ObjectName == "LargeAlienPlant" ||
                o.ObjectName == "SmallAlienPlant"),
            "Scene8 should use rainforest vegetation assets.");
        Assert.IsTrue(world.WorldInhabitants.Any(o => o.ObjectName == "BambooHut"),
            "Scene8 should use rainforest structure assets.");
        Assert.IsFalse(world.WorldInhabitants.Any(o => o.ObjectName == "Tree"),
            "Scene8 should not use the hills/woods tree assets.");
        Assert.IsFalse(world.WorldInhabitants.Any(o => o.ObjectName == "House"),
            "Scene8 should not use the hills/woods house assets.");
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; } = new GameEventBus();
        public bool IsPaused { get; set; }
    }
}
