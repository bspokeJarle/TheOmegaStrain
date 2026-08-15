using static Domain._3dSpecificsImplementations;

namespace CommonUtilities.CommonSetup
{
    //TODO: Expand this as needed, common map properties can go here
    public static class SurfaceSetup
    {
        public const int DefaultViewPortSize = 18;
        private const float OriginalTileSize = 75f;
        private const float SurfaceScreenRatio = 1.05f;

        public static int tileSize => (int)(ScreenSetup.screenSizeX * SurfaceScreenRatio / DefaultViewPortSize);
        public static int surfaceWidth => DefaultViewPortSize * tileSize;
        public static int viewPortSize => DefaultViewPortSize;
        public static float WorldScale => tileSize / OriginalTileSize;

        public static Vector3 DefaultMapPosition => new()
        {
            x = 95100f / OriginalTileSize * tileSize,
            y = 0,
            z = 95200f / OriginalTileSize * tileSize
        };
    }
}
