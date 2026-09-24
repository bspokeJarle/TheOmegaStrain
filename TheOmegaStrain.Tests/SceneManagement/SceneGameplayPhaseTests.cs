using System.Reflection;
using System.Diagnostics;
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

    [TestMethod]
    public void StartupWarmup_CountsDownAndRestoresPlanetBriefingOnce()
    {
        var handler = PrepareScene(SceneTypes.Game);
        var world = new OverlayTestWorld(handler);
        var scene = handler.GetActiveScene();
        string planetTitle = GameState.ScreenOverlayState.Title;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(SceneHandler).GetMethod("StartStartupWarmup", flags)!.Invoke(handler, [scene]);

        var overlay = GameState.ScreenOverlayState;
        Assert.AreEqual("5", overlay.Title);
        Assert.AreEqual("WARMING UP YOUR ENGINES", overlay.Body);
        Assert.IsTrue(overlay.BlocksGameplayInput);
        Assert.IsFalse(overlay.CanDismissWithInput);

        handler.HandleKeyPress(GameInputKey.Return, world);
        handler.HandleOverlayActivation(world);
        Assert.AreEqual(GamePhase.Intro, GameState.GamePlayState.Phase);
        Assert.AreEqual("5", overlay.Title);

        var startField = typeof(SceneHandler).GetField("_startupWarmupStartedTicks", flags)!;
        startField.SetValue(handler, Stopwatch.GetTimestamp() - (long)(2.25 * Stopwatch.Frequency));
        handler.UpdateFrame(world);
        Assert.AreEqual("3", overlay.Title);
        Assert.IsTrue(overlay.TitleScale < 2.4f && overlay.TitleScale > 2.0f);

        startField.SetValue(handler, Stopwatch.GetTimestamp() - (long)(5.1 * Stopwatch.Frequency));
        handler.UpdateFrame(world);
        Assert.AreEqual(planetTitle, overlay.Title);
        Assert.AreEqual(1f, overlay.TitleScale);
        Assert.IsTrue(overlay.CanDismissWithInput);

        typeof(SceneHandler).GetMethod("StartStartupWarmup", flags)!.Invoke(handler, [scene]);
        Assert.AreEqual(planetTitle, overlay.Title, "The countdown is only for the first gameplay scene.");
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
