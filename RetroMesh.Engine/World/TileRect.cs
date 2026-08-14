namespace RetroMesh.Engine
{
    public readonly record struct TileRect(int MinX, int MinZ, int MaxX, int MaxZ)
    {
        public bool Contains(int x, int z)
        {
            return x >= MinX && x <= MaxX &&
                   z >= MinZ && z <= MaxZ;
        }

        public TileRect Expand(int buffer)
        {
            return new TileRect(MinX - buffer, MinZ - buffer, MaxX + buffer, MaxZ + buffer);
        }

        public TileRect Clamp(int maxX, int maxZ)
        {
            if (maxX < 0 || maxZ < 0)
                return new TileRect(0, 0, 0, 0);

            return new TileRect(
                Math.Clamp(MinX, 0, maxX),
                Math.Clamp(MinZ, 0, maxZ),
                Math.Clamp(MaxX, 0, maxX),
                Math.Clamp(MaxZ, 0, maxZ));
        }
    }
}
