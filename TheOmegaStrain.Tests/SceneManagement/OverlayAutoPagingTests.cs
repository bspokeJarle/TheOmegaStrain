using _3dRotations.Scenes.Intro;
using _3dTesting.MainWindowClasses;
using CommonUtilities.CommonGlobalState;
using Domain;

namespace _3DSpesificsUnitTests.SceneManagement;

[TestClass]
public class OverlayAutoPagingTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.ScreenOverlayState = new ScreenOverlayState();
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
        Assert.IsTrue(text.Contains("PRESS ARROW KEYS TO NAVIGATE"));
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
    public void IntroOverlay_AutoPagesAfterLogoShowsOverlay()
    {
        var intro = new Intro();
        intro.SetupSceneOverlay();
        var overlay = GameState.ScreenOverlayState;

        Assert.IsTrue(overlay.HasMultiplePages);
        Assert.AreEqual(ScreenOverlayState.DefaultAutoPageSeconds, overlay.AutoPageSeconds);
        Assert.IsFalse(overlay.ShowOverlay, "Intro logo should still hide overlay initially.");

        overlay.ShowOverlay = true;
        overlay.Update(0.016f);
        overlay.Update(ScreenOverlayState.DefaultAutoPageSeconds + 0.016f);

        Assert.AreEqual(1, overlay.CurrentPage);
        Assert.AreEqual("FLIGHT CONTROLS", overlay.Title);
    }
}
