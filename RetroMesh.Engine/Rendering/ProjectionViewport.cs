namespace RetroMesh.Engine
{
    public interface IProjectionViewport
    {
        int ScreenWidth { get; }
        int ScreenHeight { get; }
        int ScreenCenterX { get; }
        int ScreenCenterY { get; }
        double PerspectiveAdjustment { get; }
        double ObjectZoom { get; }
    }

    public sealed class ProjectionViewport : IProjectionViewport
    {
        public ProjectionViewport(
            int screenWidth,
            int screenHeight,
            double perspectiveAdjustment,
            double objectZoom)
        {
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            PerspectiveAdjustment = perspectiveAdjustment;
            ObjectZoom = objectZoom;
        }

        public int ScreenWidth { get; }
        public int ScreenHeight { get; }
        public int ScreenCenterX => ScreenWidth / 2;
        public int ScreenCenterY => ScreenHeight / 2;
        public double PerspectiveAdjustment { get; }
        public double ObjectZoom { get; }
    }
}
