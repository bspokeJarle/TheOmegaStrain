using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Persistence;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.SceneManagement;
using TheOmegaStrain.Game.Scenes.Intro;
using TheOmegaStrain.Game.World;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class IntroLanguageMenuTests
{
    private string _originalFolder = "";
    private string _testFolder = "";

    [TestInitialize]
    public void Setup()
    {
        _originalFolder = PersistenceSetup.LocalFolder;
        _testFolder = Path.Combine(Path.GetTempPath(), "OmegaIntroLanguageTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testFolder;
        PersistenceSetup.Initialize();
        GameState.SettingsState = new GameSettingsState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.WorldFade = new WorldFadeState();
    }

    [TestCleanup]
    public void Cleanup()
    {
        PersistenceSetup.LocalFolder = _originalFolder;
        if (Directory.Exists(_testFolder))
            Directory.Delete(_testFolder, recursive: true);
    }

    [TestMethod]
    public void ConfirmLanguageOption_CyclesAndPersistsWithoutStartingGame()
    {
        var handler = new SceneHandler();
        var world = new GameWorld { SceneHandler = handler };
        handler.GetActiveScene().SetupSceneOverlay();
        var overlay = GameState.ScreenOverlayState;
        overlay.ShowOverlay = true;
        overlay.MoveChoiceSelection(Intro.LanguageChoiceIndex);

        handler.HandleKeyPress(GameInputKey.Enter, world);

        Assert.AreEqual("no", GameState.SettingsState.LanguageCode);
        Assert.AreEqual("no", GameSettingsPersistence.LoadSettings().LanguageCode);
        Assert.AreEqual(Intro.LanguageChoiceIndex, overlay.SelectedChoiceIndex);
        Assert.AreEqual("START SPILLET", overlay.ChoiceOptions[0]);
        Assert.AreEqual("THE OMEGA STRAIN // ORIENTERING", overlay.Pages[1][1]);
        Assert.AreEqual("TIPS OG TRIKS", overlay.Pages[2][1]);
        Assert.AreEqual(ScreenOverlayChoiceAction.IntroMainMenu, overlay.ChoiceAction);

        for (int i = 0; i < 4; i++)
            handler.HandleKeyPress(GameInputKey.Enter, world);

        Assert.AreEqual("en", GameState.SettingsState.LanguageCode);
        Assert.AreEqual("START GAME", overlay.ChoiceOptions[0]);
        Assert.AreEqual("THE OMEGA STRAIN // BRIEFING", overlay.Pages[1][1]);
    }

    [TestMethod]
    public void LeftRight_OnFlags_ChangesLanguageButNotPage()
    {
        var handler = new SceneHandler();
        var world = new GameWorld { SceneHandler = handler };
        handler.GetActiveScene().SetupSceneOverlay();
        var overlay = GameState.ScreenOverlayState;
        overlay.ShowOverlay = true;
        overlay.MoveChoiceSelection(Intro.LanguageChoiceIndex);

        handler.HandleKeyPress(GameInputKey.Left, world);

        Assert.AreEqual("pl", GameState.SettingsState.LanguageCode);
        Assert.AreEqual("pl", GameSettingsPersistence.LoadSettings().LanguageCode);
        Assert.AreEqual(0, overlay.CurrentPage);
        Assert.AreEqual(Intro.LanguageChoiceIndex, overlay.SelectedChoiceIndex);

        handler.HandleKeyPress(GameInputKey.Right, world);
        Assert.AreEqual("en", GameState.SettingsState.LanguageCode);
        Assert.AreEqual(0, overlay.CurrentPage);

        handler.HandleKeyPress(GameInputKey.Right, world);
        Assert.AreEqual("no", GameState.SettingsState.LanguageCode);
        overlay.MoveChoiceSelection(-3);
        handler.HandleKeyPress(GameInputKey.Right, world);
        Assert.AreEqual(0, overlay.CurrentPage);
        overlay.MoveChoiceSelection(Intro.InfoChoiceIndex - overlay.SelectedChoiceIndex);
        handler.HandleKeyPress(GameInputKey.Right, world);
        Assert.AreEqual("THE OMEGA STRAIN // ORIENTERING", overlay.Title);
    }

    [TestMethod]
    public void InfoPages_OpenOnlyFromInfoMenuChoice()
    {
        var handler = new SceneHandler();
        var world = new GameWorld { SceneHandler = handler };
        handler.GetActiveScene().SetupSceneOverlay();
        var overlay = GameState.ScreenOverlayState;
        overlay.ShowOverlay = true;

        handler.HandleKeyPress(GameInputKey.Right, world);
        Assert.AreEqual(0, overlay.CurrentPage);
        Assert.AreEqual(ScreenOverlayChoiceAction.IntroMainMenu, overlay.ChoiceAction);

        overlay.MoveChoiceSelection(Intro.InfoChoiceIndex);
        handler.HandleKeyPress(GameInputKey.Right, world);
        Assert.AreEqual(1, overlay.CurrentPage);
        Assert.AreEqual(ScreenOverlayChoiceAction.None, overlay.ChoiceAction);

        handler.HandleKeyPress(GameInputKey.Left, world);
        Assert.AreEqual(0, overlay.CurrentPage);
        Assert.AreEqual(ScreenOverlayChoiceAction.IntroMainMenu, overlay.ChoiceAction);
    }

    [TestMethod]
    public void Quit_IsLastChoiceWithVisualSeparationAndOpensConfirmation()
    {
        var handler = new SceneHandler();
        var world = new GameWorld { SceneHandler = handler };
        handler.GetActiveScene().SetupSceneOverlay();
        var overlay = GameState.ScreenOverlayState;
        overlay.ShowOverlay = true;

        Assert.AreEqual(overlay.ChoiceOptions.Count - 1, Intro.QuitChoiceIndex);
        Assert.AreEqual("QUIT", overlay.ChoiceOptions[Intro.QuitChoiceIndex]);
        Assert.IsTrue(overlay.Body.Contains("  VIEW INFO PAGES\n\n  QUIT"));

        overlay.MoveChoiceSelection(Intro.QuitChoiceIndex);
        handler.HandleKeyPress(GameInputKey.Enter, world);

        Assert.AreEqual(ScreenOverlayChoiceAction.QuitGameConfirmation, overlay.ChoiceAction);
    }
}
