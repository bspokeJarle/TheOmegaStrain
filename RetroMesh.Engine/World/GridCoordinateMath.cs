namespace Domain
{
    public static class GridCoordinateMath
    {
        public static int WrapIndex(int index, int size)
        {
            if (size <= 0)
                return 0;

            int wrapped = index % size;
            return wrapped < 0 ? wrapped + size : wrapped;
        }

        public static int GetWrappedRelativeIndex(int index, int origin, int size)
        {
            return WrapIndex(index - origin, size);
        }

        public static int WorldToTileIndex(float worldCoordinate, int tileSize, int tileCount)
        {
            if (tileSize <= 0)
                return 0;

            int tileIndex = (int)MathF.Floor(worldCoordinate / tileSize);
            return WrapIndex(tileIndex, tileCount);
        }

        public static TileRect GetCenteredTileRect(
            int tileCountX,
            int tileCountZ,
            int rectSizeX,
            int rectSizeZ,
            int bufferTiles = 0)
        {
            if (tileCountX <= 0 || tileCountZ <= 0 || rectSizeX <= 0 || rectSizeZ <= 0)
                return new TileRect(0, 0, 0, 0);

            int centerX = tileCountX / 2;
            int centerZ = tileCountZ / 2;
            int halfX = rectSizeX / 2;
            int halfZ = rectSizeZ / 2;

            var rect = new TileRect(
                Math.Max(0, centerX - halfX),
                Math.Max(0, centerZ - halfZ),
                Math.Min(tileCountX - 1, centerX - halfX + rectSizeX - 1),
                Math.Min(tileCountZ - 1, centerZ - halfZ + rectSizeZ - 1));

            return bufferTiles <= 0
                ? rect
                : rect.Expand(bufferTiles).Clamp(tileCountX - 1, tileCountZ - 1);
        }

        public static (int x, int z) GetCenterTile(int tileCountX, int tileCountZ)
        {
            return (Math.Max(0, tileCountX / 2), Math.Max(0, tileCountZ / 2));
        }
    }
}
