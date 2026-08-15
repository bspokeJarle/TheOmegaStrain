using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

using RetroMesh.Engine;

namespace TheOmegaStrain.Common.GamePlayHelpers
{
    public static class MapCoordinateHelpers
    {
        public static int WrapIndex(int index, int size)
        {
            return GridCoordinateMath.WrapIndex(index, size);
        }

        public static int GetWrappedRelativeIndex(int index, int origin, int size)
        {
            return GridCoordinateMath.GetWrappedRelativeIndex(index, origin, size);
        }

        public static int WorldToTileIndex(float worldCoordinate, int tileSize, int tileCount)
        {
            return GridCoordinateMath.WorldToTileIndex(worldCoordinate, tileSize, tileCount);
        }

        public static int WorldXToTileIndex(float worldX, SurfaceData[,] map)
        {
            return WorldToTileIndex(worldX, SurfaceSetup.tileSize, map.GetLength(1));
        }

        public static int WorldZToTileIndex(float worldZ, SurfaceData[,] map)
        {
            return WorldToTileIndex(worldZ, SurfaceSetup.tileSize, map.GetLength(0));
        }
    }
}
