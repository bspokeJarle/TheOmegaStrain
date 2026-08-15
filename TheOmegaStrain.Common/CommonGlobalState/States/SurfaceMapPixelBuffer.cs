namespace CommonUtilities.CommonGlobalState.States
{
    public sealed class SurfaceMapPixelBuffer
    {
        public SurfaceMapPixelBuffer(int width, int height)
        {
            Width = width;
            Height = height;
            Stride = width * 4;
            Pixels = new byte[Stride * height];
        }

        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public byte[] Pixels { get; }
        public int Version { get; set; }
        public object SyncRoot { get; } = new();

        public bool HasSize(int width, int height)
        {
            return Width == width && Height == height;
        }
    }
}
