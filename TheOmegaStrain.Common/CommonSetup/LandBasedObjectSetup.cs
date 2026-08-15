namespace CommonUtilities.CommonSetup
{
    public static class LandBasedObjectSetup
    {
        public const float WinterSurfaceObjectScale = 1.20f;
        public const float WinterGroundContactNudgeY = 12f;
        public static float GroundContactNudgeY = 6f;
        public const float SurfaceFootprintOffsetY = 500f;

        public static float GroundContactNudgeYScaled => GroundContactNudgeY * ScreenSetup.ScreenScaleY;
        public static float SurfaceFootprintOffsetYScaled => SurfaceFootprintOffsetY * ScreenSetup.ScreenScaleY;
        public static float NudgedSurfaceFootprintOffsetYScaled => (SurfaceFootprintOffsetY - GroundContactNudgeY) * ScreenSetup.ScreenScaleY;
        public static float WinterSurfaceFootprintOffsetYScaled => (SurfaceFootprintOffsetY - WinterGroundContactNudgeY) * ScreenSetup.ScreenScaleY;
    }
}
