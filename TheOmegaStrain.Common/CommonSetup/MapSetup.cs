using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonUtilities.CommonSetup
{
    //TODO: Expand this as needed, common map properties can go here
    public static class MapSetup
    {
        private const float OriginalTileSize = 75f;
        public const int globalMapSize = 2500 + SurfaceSetup.DefaultViewPortSize;
        public static int tileSize => SurfaceSetup.tileSize;
        public static int maxHeight = 75; //Height elevation for the map
        public static int bitmapMapCenterOffsetX => (int)(4700f / OriginalTileSize * SurfaceSetup.tileSize);
        public static int bitmapMapCenterOffsetY => (int)(1750f / OriginalTileSize * SurfaceSetup.tileSize);
        public const int bitmapSize = 72; //Size of the bitmap on screen
        public static int screensPrMap => globalMapSize / SurfaceSetup.DefaultViewPortSize;
    }
}
