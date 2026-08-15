using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Common.GamePlayHelpers
{
    public static class LandingPlatformHelpers
    {
        public const int LandingPlatformSizeTiles = 8;

        public static TileRect GetLandingPlatformRect(SurfaceData[,] map, int bufferTiles = 0)
        {
            return GridCoordinateMath.GetCenteredTileRect(
                map.GetLength(1),
                map.GetLength(0),
                LandingPlatformSizeTiles,
                LandingPlatformSizeTiles,
                bufferTiles);
        }

        public static (int x, int z) GetLandingPlatformCenterTile(SurfaceData[,] map)
        {
            return GridCoordinateMath.GetCenterTile(map.GetLength(1), map.GetLength(0));
        }

        public static bool IsLandingPlatformTile(SurfaceData[,] map, int x, int z, int bufferTiles = 0)
        {
            return GetLandingPlatformRect(map, bufferTiles).Contains(x, z);
        }

        public static bool IsSurfaceBasedOnLandingPlatform(SurfaceData[,] map, int surfaceBasedId, int bufferTiles = 0)
        {
            if (surfaceBasedId <= 0)
                return false;

            var rect = GetLandingPlatformRect(map, bufferTiles);
            for (int z = rect.MinZ; z <= rect.MaxZ; z++)
            {
                for (int x = rect.MinX; x <= rect.MaxX; x++)
                {
                    if (map[z, x].mapId == surfaceBasedId)
                        return true;
                }
            }

            return false;
        }
    }
}
