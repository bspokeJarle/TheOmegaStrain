using CommonUtilities.CommonGlobalState;
using Domain;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CommonUtilities.GamePlayHelpers
{
    public static class MapHelpers
    {
        public static void UpdateTilePixel(WriteableBitmap bitmap, int tileX, int tileZ, Color color)
        {
            // 1 pixel (BGRA)
            byte[] px = { color.B, color.G, color.R, 255 };

            // NB: Int32Rect(x,y,w,h) -> x=tileX, y=tileZ
            bitmap.WritePixels(new Int32Rect(tileX, tileZ, 1, 1), px, 4, 0);
        }

        public static void UpdateTerrainBitmap(SurfaceData[,] terrainMap, int mapSize, int maxHeight, bool enableLogging = false)
        {
            WriteableBitmap wb = GameState.SurfaceState.GlobalMapBitmap as WriteableBitmap;

            try
            {
                if (wb == null || wb.PixelWidth != mapSize || wb.PixelHeight != mapSize)
                {
                    if (Logger.ShouldLog(enableLogging))
                        Logger.Log($"UpdateTerrainBitmap: recreating bitmap (existing={(wb == null ? "null" : $"{wb.PixelWidth}x{wb.PixelHeight}")})", "MapHelpers");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        wb = new WriteableBitmap(mapSize, mapSize, 96, 96, PixelFormats.Bgra32, null);
                        GameState.SurfaceState.GlobalMapBitmap = wb;
                    });
                }
            }
            catch (Exception ex)
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log($"Error creating WriteableBitmap: {ex.Message}", "MapHelpers");
                return;
            }

            int stride = mapSize * 4;
            byte[] pixelData = new byte[mapSize * mapSize * 4];

            for (int i = 0; i < mapSize; i++)
            {
                for (int j = 0; j < mapSize; j++)
                {
                    TerrainPaletteHelpers.GetTerrainColorRgb(
                        terrainMap[i, j].mapDepth,
                        maxHeight,
                        GameState.SurfaceState.SceneBiome,
                        out int red,
                        out int green,
                        out int blue);

                    int index = (i * mapSize + j) * 4;
                    pixelData[index] = (byte)blue;
                    pixelData[index + 1] = (byte)green;
                    pixelData[index + 2] = (byte)red;
                    pixelData[index + 3] = 255;
                }
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var currentWb = GameState.SurfaceState.GlobalMapBitmap as WriteableBitmap;
                if (currentWb != null && currentWb.PixelWidth == mapSize && currentWb.PixelHeight == mapSize)
                {
                    currentWb.WritePixels(new Int32Rect(0, 0, mapSize, mapSize), pixelData, stride, 0);
                }
                else if (Logger.ShouldLog(enableLogging))
                {
                    Logger.Log("UpdateTerrainBitmap: bitmap size mismatch; skipping WritePixels", "MapHelpers");
                }
            }), DispatcherPriority.Render);
        }
    }
}
