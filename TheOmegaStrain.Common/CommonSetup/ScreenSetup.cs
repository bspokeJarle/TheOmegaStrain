namespace CommonUtilities.CommonSetup
{
    //TODO: Expand this as needed, common Screen properties can go here
    public static class ScreenSetup
    {
        public const int perspectiveAdjustment = 1500;
        public const int defaultObjectZoom = 2;
        public const int targetFps = 90;
        public static int RuntimeTargetFps { get; private set; } = targetFps;
        public static double TargetFrameIntervalMs => 1000.0 / RuntimeTargetFps;

        // Screen dimensions — initialized at startup from the actual window size.
        // Default values match the original constants so the engine works even
        // before Initialize() is called (unit tests, benchmarks, etc.).
        public static int screenSizeX { get; private set; } = 1500;
        public static int screenSizeY { get; private set; } = 1024;

        // Original design dimensions (scaling base for pixel-based offsets)
        private const float DesignWidth = 1500f;
        private const float DesignHeight = 1024f;
        public static float ScreenScaleX => screenSizeX / DesignWidth;
        public static float ScreenScaleY => screenSizeY / DesignHeight;

        // Depth / view-distance constants
        public const float RenderFarZ = 2000f;
        public const float RenderNearZ = -1750f;
        public const float ObjectVisibilityDistance = 2000f;

        /// <summary>
        /// Call once at startup with the actual DPI-scaled rendering size of the window.
        /// </summary>
        public static void Initialize(int width, int height)
        {
            screenSizeX = width;
            screenSizeY = height;
        }

        public static void ConfigureRuntimeTargetFps(int displayRefreshHz)
        {
            if (displayRefreshHz < 30)
            {
                RuntimeTargetFps = targetFps;
                return;
            }

            RuntimeTargetFps = System.Math.Min(targetFps, NormalizeRefreshRate(displayRefreshHz));
        }

        private static int NormalizeRefreshRate(int displayRefreshHz)
        {
            return displayRefreshHz switch
            {
                59 => 60,
                119 => 120,
                143 => 144,
                _ => displayRefreshHz
            };
        }
    }
}
