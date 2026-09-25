using TheOmegaStrain.Game.Scenes.Intro;
using TheOmegaStrain.Wpf.MainWindowClasses;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Localization;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class OverlayAutoPagingTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.SettingsState = new GameSettingsState();
    }

    [TestMethod]
    public void AddPage_EnablesAutoPagingWhenOverlayHasMultiplePages()
    {
        var overlay = new ScreenOverlayState();

        overlay.AddPage("H1", "T1", "B1", "F1");

        Assert.AreEqual(0f, overlay.AutoPageSeconds,
            "Single-page overlays should not auto-page.");

        overlay.AddPage("H2", "T2", "B2", "F2");

        Assert.AreEqual(ScreenOverlayState.DefaultAutoPageSeconds, overlay.AutoPageSeconds,
            "Any overlay with multiple pages should auto-page by default.");
    }

    [TestMethod]
    public void DefaultAutoPageSeconds_IsLongEnoughForManualReading()
    {
        Assert.AreEqual(20f, ScreenOverlayState.DefaultAutoPageSeconds);
    }

    [TestMethod]
    public void PageIndicator_ShowsArrowKeyNavigationHint()
    {
        string text = OverlayHandler.BuildPageIndicatorText(totalPages: 3, currentPage: 1);

        Assert.IsTrue(text.Contains("[*]"));
        Assert.IsTrue(text.Contains("LEFT/RIGHT TO NAVIGATE"));
    }

    [TestMethod]
    public void SettingsPageIndicator_ShowsActivePageAndDedicatedNavigationHint()
    {
        string text = OverlayHandler.BuildSettingsPageIndicatorText(ScreenOverlaySettingsPanel.Graphics);

        Assert.AreEqual(1, text.Split("[*]").Length - 1);
        Assert.AreEqual(4, text.Split("[").Length - 1);
        Assert.IsTrue(text.Contains("PAGE UP/DOWN TO CHANGE SETTINGS PAGE"));
        Assert.IsFalse(text.Contains("LEFT/RIGHT TO NAVIGATE"));
    }

    [TestMethod]
    public void Update_AutoPagesVisibleMultiPageOverlay()
    {
        var overlay = new ScreenOverlayState();
        overlay.AddPage("H1", "T1", "B1", "F1");
        overlay.AddPage("H2", "T2", "B2", "F2");
        overlay.CurrentPage = 0;
        overlay.ApplyPageContent();
        overlay.ShowOverlay = true;

        overlay.Update(0.016f);
        overlay.Update(ScreenOverlayState.DefaultAutoPageSeconds + 0.016f);

        Assert.AreEqual(1, overlay.CurrentPage);
        Assert.AreEqual("T2", overlay.Title);
    }

    [TestMethod]
    public void TrySelectPageByTitle_SelectsMatchingPageAndAppliesContent()
    {
        var overlay = new ScreenOverlayState();
        overlay.AddPage("H1", "T1", "B1", "F1");
        overlay.AddPage("H2", "FLIGHT CONTROLS", "B2", "F2");
        overlay.CurrentPage = 0;
        overlay.ApplyPageContent();

        bool selected = overlay.TrySelectPageByTitle("FLIGHT CONTROLS");

        Assert.IsTrue(selected);
        Assert.AreEqual(1, overlay.CurrentPage);
        Assert.AreEqual("FLIGHT CONTROLS", overlay.Title);
        Assert.AreEqual("B2", overlay.Body);
        Assert.AreEqual("F2", overlay.Footer);
    }

    [TestMethod]
    public void IntroOverlay_MainMenuDoesNotAutoPage()
    {
        var intro = new Intro();
        intro.SetupSceneOverlay();
        var overlay = GameState.ScreenOverlayState;

        Assert.IsTrue(overlay.HasMultiplePages);
        Assert.AreEqual(0f, overlay.AutoPageSeconds);
        Assert.IsFalse(overlay.ShowOverlay, "Intro logo should still hide overlay initially.");
        Assert.AreEqual(ScreenOverlayChoiceAction.IntroMainMenu, overlay.ChoiceAction);
        Assert.AreEqual("THE OMEGA STRAIN", overlay.Title);

        overlay.ShowOverlay = true;
        overlay.Update(0.016f);
        overlay.Update(ScreenOverlayState.DefaultAutoPageSeconds + 0.016f);

        Assert.AreEqual(0, overlay.CurrentPage);
    }

    [TestMethod]
    public void RefreshControlFooter_PreservesMainMenuSelection()
    {
        var intro = new Intro();
        intro.SetupSceneOverlay();
        var overlay = GameState.ScreenOverlayState;
        overlay.MoveChoiceSelection(1);

        Intro.RefreshControlFooter();

        Assert.AreEqual(1, overlay.SelectedChoiceIndex);
        Assert.AreEqual("TRAINING", overlay.SelectedChoice);
    }

    [TestMethod]
    public void IntroOverlay_UsesSelectedLanguageAndEnglishFallback()
    {
        GameState.SettingsState.LanguageCode = "pl";
        new Intro().SetupSceneOverlay();

        var overlay = GameState.ScreenOverlayState;
        Assert.AreEqual("ROZPOCZNIJ GRĘ", overlay.ChoiceOptions[0]);
        StringAssert.Contains(overlay.ChoiceBodyPrefix, "JĘZYK");
        Assert.AreEqual("missing.key", GameText.Get("missing.key", "pl"));
        Assert.AreEqual("START GAME", GameText.Get("menu.start", "unsupported"));
    }
}
