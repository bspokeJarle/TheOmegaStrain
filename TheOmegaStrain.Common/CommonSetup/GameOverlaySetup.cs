namespace CommonUtilities.CommonSetup
{
    public static class GameOverlaySetup
    {
        public const double HudHeightRatio = 0.25;
        public const float GuidanceArrowHudClearanceY = 85f;

        public static float HudHeight => (float)(ScreenSetup.screenSizeY * HudHeightRatio);

        public static float GuidanceArrowMinimumScreenY =>
            HudHeight + GuidanceArrowHudClearanceY * ScreenSetup.ScreenScaleY;

        public static float AnchorScreenYBelowHud(float preferredScreenY)
        {
            return System.Math.Max(preferredScreenY, GuidanceArrowMinimumScreenY);
        }
    }
}
