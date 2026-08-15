using CommonUtilities.CommonGlobalState;
using Domain;
using System.Windows.Controls;

namespace _3dTesting.MainWindowClasses.Overlays
{
    /// <summary>
    /// Central coordinator for all overlay handlers.
    /// Owns the text-based modal overlay and the in-game HUD.
    /// Provides a single Update() call instead of managing handlers individually.
    /// </summary>
    public sealed class OverlayManager
    {
        private readonly OverlayHandler _textOverlay;
        private readonly HudOverlayHandlerV2 _hud;

        public OverlayManager(Grid root)
        {
            _textOverlay = new OverlayHandler(root);
            _hud = new HudOverlayHandlerV2(root);
        }

        /// <summary>
        /// Updates all overlay handlers for the current frame.
        /// </summary>
        public void Update(
            ScreenOverlayState overlayState,
            GamePlayState gameplay,
            double screenWidth,
            double screenHeight,
            int fps,
            int triangles)
        {
            _textOverlay.Update(overlayState, screenWidth, screenHeight);
            _hud.Update(overlayState, gameplay, screenWidth, screenHeight, fps, triangles);
        }

        /// <summary>
        /// Returns the minimap Image element for marker rendering.
        /// </summary>
        public Image GetMinimapImage() => _hud.GetMinimapImage();
    }
}
