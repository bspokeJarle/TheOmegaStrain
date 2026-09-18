using System.Reflection;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.SceneManagement;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
[DoNotParallelize]
public class SceneGameplayPhaseTests
{
    private GamePlayState _previousGameplay = null!;
    private ScreenOverlayState _previousOverlay = null!;

    [TestInitialize]
    public void Setup()
    {
        _previousGameplay = GameState.GamePlayState;
        _previousOverlay = GameState.ScreenOverlayState;
        GameState.GamePlayState = new GamePlayState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.GamePlayState = _previousGameplay;
        GameState.ScreenOverlayState = _previousOverlay;
    }

    [DataTestMethod]
    [DataRow(SceneTypes.Game, false)]
    [DataRow(SceneTypes.Game, true)]
    [DataRow(SceneTypes.Simulation, false)]
    [DataRow(SceneTypes.Simulation, true)]
    public void ClosingPlanetIntro_StartsPlaying(SceneTypes type, bool overlayActivation)
    {
        var handler = PrepareScene(type);
        var world = new OverlayTestWorld(handler);
        Assert.AreEqual(GamePhase.Intro, GameState.GamePlayState.Phase);
        Assert.IsTrue(GameState.ScreenOverlayState.ShowOverlay);

        if (overlayActivation) handler.HandleOverlayActivation(world);
        else handler.HandleKeyPress(GameInputKey.Return, world);

        Assert.AreEqual(ScreenOverlayType.Game, GameState.ScreenOverlayState.Type);
        Assert.IsFalse(GameState.ScreenOverlayState.ShowOverlay);
        Assert.AreEqual(GamePhase.Playing, GameState.GamePlayState.Phase);
        Assert.IsTrue(GameState.GamePlayState.IsPlaying, "Warning detection and HUD must see actual gameplay.");
    }

    [TestMethod]
    public void BrowsingPlanetIntro_DoesNotStartPlaying()
    {
        var handler = PrepareScene(SceneTypes.Game);
        var overlay = GameState.ScreenOverlayState;
        overlay.AddPage("First", "First", "Briefing", "Next");
        overlay.AddPage("Second", "Second", "Briefing", "Start");
        handler.HandleKeyPress(GameInputKey.Right, new OverlayTestWorld(handler));
        Assert.AreEqual(GamePhase.Intro, GameState.GamePlayState.Phase);
        Assert.IsTrue(overlay.ShowOverlay);
    }

    [TestMethod]
    public void BlockedOverlayActivation_DoesNotStartPlaying()
    {
        var handler = PrepareScene(SceneTypes.Game);
        GameState.ScreenOverlayState.CanDismissWithInput = false;
        handler.HandleOverlayActivation(new OverlayTestWorld(handler));
        Assert.AreEqual(GamePhase.Intro, GameState.GamePlayState.Phase);
        Assert.IsTrue(GameState.ScreenOverlayState.ShowOverlay);
    }

    private static SceneHandler PrepareScene(SceneTypes type)
    {
        var handler = new SceneHandler();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var scenes = (List<IScene>)typeof(SceneHandler).GetField("scenes", flags)!.GetValue(handler)!;
        int index = scenes.FindIndex(scene => scene.SceneType == type);
        Assert.IsTrue(index >= 0);
        typeof(SceneHandler).GetField("currentSceneIndex", flags)!.SetValue(handler, index);
        GameState.GamePlayState.ResetForNewGame();
        GameState.GamePlayState.SceneIndex = index;
        GameState.GamePlayState.CurrentSceneType = type;
        // Exercise real scene overlays without generating the terrain or loading saves.
        handler.GetActiveScene().SetupSceneOverlay();
        return handler;
    }

    private sealed class OverlayTestWorld(ISceneHandler handler) : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = handler;
        public IGameEventBus? EventBus { get; set; }
        public bool IsPaused { get; set; }
    }
}
