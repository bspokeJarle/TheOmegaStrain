using TheOmegaStrain.Game.Scenes.Scene3;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Domain;
using System.Linq;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class Scene3RainforestWeatherTests
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
    public void SetupScene_AddsRainAndLightningEmittersOnlyForRainforestBiome()
    {
        var scene = new Scene3();
        var world = new TestWorld();

        scene.SetupScene(world);

        var rainEmitters = world.WorldInhabitants.Where(o => o.ObjectName == "RainEmitter").ToList();
        var lightningEmitters = world.WorldInhabitants.Where(o => o.ObjectName == "LightningEmitter").ToList();
        var snowEmitters = world.WorldInhabitants.Where(o => o.ObjectName == "SnowEmitter").ToList();

        Assert.AreEqual(SceneBiomeTypes.Rainforrest, scene.SceneBiome);
        Assert.AreEqual(1, rainEmitters.Count, "Rainforest scene should add exactly one rain emitter.");
        Assert.AreEqual(1, lightningEmitters.Count, "Rainforest scene should add exactly one lightning emitter.");
        Assert.AreEqual(0, snowEmitters.Count, "Rainforest scene should not add the winter snow emitter.");
        Assert.AreEqual(0, rainEmitters[0].CrashBoxes.Count, "Rain should not participate in crash detection.");
        Assert.AreEqual(0, lightningEmitters[0].CrashBoxes.Count, "Lightning should not participate in crash detection.");
        Assert.IsNull(rainEmitters[0].Particles, "Rain is rendered by the scene-owned emitter, not the global particle system.");
        Assert.IsNull(lightningEmitters[0].Particles, "Lightning is rendered by the scene-owned emitter, not the global particle system.");
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; } = new GameEventBus();
        public bool IsPaused { get; set; }
    }
}
