using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Common.GamePlayHelpers
{
    public static class MapHelpers
    {
        public static void UpdateTilePixel(SurfaceMapPixelBuffer buffer, int tileX, int tileZ, byte blue, byte green, byte red, byte alpha = 255)
        {
            if (buffer == null)
                return;

            if (tileX < 0 || tileZ < 0 || tileX >= buffer.Width || tileZ >= buffer.Height)
                return;

            lock (buffer.SyncRoot)
            {
                int index = (tileZ * buffer.Stride) + (tileX * 4);
                buffer.Pixels[index] = blue;
                buffer.Pixels[index + 1] = green;
                buffer.Pixels[index + 2] = red;
                buffer.Pixels[index + 3] = alpha;
                buffer.Version++;
            }
        }

        public static void UpdateTerrainMapPixels(SurfaceData[,] terrainMap, int mapSize, int maxHeight, bool enableLogging = false)
        {
            SurfaceMapPixelBuffer? buffer = GameState.SurfaceState.GlobalMapPixels;
            if (buffer == null || !buffer.HasSize(mapSize, mapSize))
            {
                if (Logger.ShouldLog(enableLogging))
                    Logger.Log($"UpdateTerrainMapPixels: recreating pixel buffer (existing={(buffer == null ? "null" : $"{buffer.Width}x{buffer.Height}")})", "MapHelpers");

                buffer = new SurfaceMapPixelBuffer(mapSize, mapSize);
                GameState.SurfaceState.GlobalMapPixels = buffer;
            }

            lock (buffer.SyncRoot)
            {
                for (int z = 0; z < mapSize; z++)
                {
                    for (int x = 0; x < mapSize; x++)
                    {
                        TerrainPaletteHelpers.GetTerrainColorRgb(
                            terrainMap[z, x].mapDepth,
                            maxHeight,
                            GameState.SurfaceState.SceneBiome,
                            out int red,
                            out int green,
                            out int blue);

                        int index = (z * buffer.Stride) + (x * 4);
                        buffer.Pixels[index] = (byte)blue;
                        buffer.Pixels[index + 1] = (byte)green;
                        buffer.Pixels[index + 2] = (byte)red;
                        buffer.Pixels[index + 3] = 255;
                    }
                }

                buffer.Version++;
            }
        }
    }
}
