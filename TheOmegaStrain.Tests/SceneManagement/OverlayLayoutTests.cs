using _3dTesting.MainWindowClasses;
using Domain;

namespace _3DSpesificsUnitTests.SceneManagement;

[TestClass]
public class OverlayLayoutTests
{
    [TestMethod]
    public void CalculatePanelHeight_TopAnchoredOverlayStaysInsideScreenAfterYOffset()
    {
        const double screenHeight = 1080.0;
        double yOffset = screenHeight * 0.18;

        double panelHeight = OverlayHandler.CalculatePanelHeight(
            desiredHeight: 1400.0,
            screenHeight,
            yOffset,
            ScreenOverlayAnchor.Top);

        Assert.IsTrue(yOffset + panelHeight <= screenHeight,
            "Top-anchored overlay panel should not extend below the screen after applying its top offset.");
        Assert.IsTrue(panelHeight < screenHeight * 0.90,
            "The height cap should account for the top offset, not only the total screen height.");
    }

    // Regression: the LiveGameLoop victory flow builds a Game-type overlay
    // (e.g. "PLANET SECURED / ALL THREATS ELIMINATED") and sets ShowOverlay = true.
    // The renderer must rely on ShouldRender (driven by Opacity) instead of
    // unconditionally collapsing Game-type overlays, otherwise the victory
    // message is built but never appears on screen.
    [TestMethod]
    public void GameOverlay_WithShowOverlayTrue_BecomesRenderable()
    {
        var state = new ScreenOverlayState();
        state.SetGameOverlayPreset("PLANET SECURED", "ALL THREATS ELIMINATED", "Proceeding to next sector...");
        state.ShowOverlay = true;

        // Fade-in: FadeInSpeed defaults to 2.5/sec, so 1.0s is more than enough
        // to push Opacity above ShouldRender's 0.001 threshold.
        state.Update(1.0f);

        Assert.AreEqual(ScreenOverlayType.Game, state.Type,
            "Victory overlay must keep the Game overlay type used by LiveGameLoop.");
        Assert.IsTrue(state.ShouldRender,
            "Game overlays with ShowOverlay = true must be renderable so the victory panel is visible.");
    }

    // Regression: the normal in-game state uses Type = Game with ShowOverlay = false
    // (HUD only, no framed text panel). That state must remain invisible after the
    // renderer drops its old Type == Game short-circuit.
    [TestMethod]
    public void GameOverlay_WithShowOverlayFalse_StaysHidden()
    {
        var state = new ScreenOverlayState();
        state.SetGameOverlayPreset("Header", "Title", "");
        state.ShowOverlay = false;

        state.Update(1.0f);

        Assert.AreEqual(ScreenOverlayType.Game, state.Type);
        Assert.IsFalse(state.ShouldRender,
            "Game overlays with ShowOverlay = false must stay hidden so only the HUD shows during normal gameplay.");
    }
}
