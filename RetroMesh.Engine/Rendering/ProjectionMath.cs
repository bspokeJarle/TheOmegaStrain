using System;

namespace RetroMesh.Engine
{
    public static class ProjectionMath
    {
        private const double NearPlaneSafetyMargin = 1.0;

        public static bool TryProjectVertex(
            IVector3 vertex,
            double objectScreenX,
            double objectScreenY,
            double objectScreenZ,
            IProjectionViewport viewport,
            out (double x, double y) screenPoint)
        {
            ArgumentNullException.ThrowIfNull(vertex);
            ArgumentNullException.ThrowIfNull(viewport);

            double denom = -vertex.z + objectScreenZ + viewport.PerspectiveAdjustment;
            if (denom <= NearPlaneSafetyMargin)
            {
                screenPoint = (double.NaN, double.NaN);
                return false;
            }

            double factor = viewport.PerspectiveAdjustment / denom;
            screenPoint = (
                vertex.x * factor * viewport.ObjectZoom + objectScreenX,
                vertex.y * factor * viewport.ObjectZoom + objectScreenY);
            return true;
        }

        public static bool IsOnScreen(
            double x,
            double y,
            IProjectionViewport viewport,
            double screenMarginFactor = 0.2)
        {
            ArgumentNullException.ThrowIfNull(viewport);

            return x >= -(viewport.ScreenWidth * screenMarginFactor)
                && x <= viewport.ScreenWidth * (1 + screenMarginFactor)
                && y >= -(viewport.ScreenHeight * screenMarginFactor)
                && y <= viewport.ScreenHeight * (1 + screenMarginFactor);
        }

        public static bool TryClampTriangleToViewport(
            ref (double x, double y) p1,
            ref (double x, double y) p2,
            ref (double x, double y) p3,
            IProjectionViewport viewport,
            double screenMarginFactor)
        {
            ArgumentNullException.ThrowIfNull(viewport);

            if (double.IsNaN(p1.x) || double.IsNaN(p1.y) ||
                double.IsNaN(p2.x) || double.IsNaN(p2.y) ||
                double.IsNaN(p3.x) || double.IsNaN(p3.y))
                return false;

            double minX = -(viewport.ScreenWidth * screenMarginFactor);
            double maxX = viewport.ScreenWidth * (1 + screenMarginFactor);
            double minY = -(viewport.ScreenHeight * screenMarginFactor);
            double maxY = viewport.ScreenHeight * (1 + screenMarginFactor);

            if ((p1.x < minX && p2.x < minX && p3.x < minX) ||
                (p1.x > maxX && p2.x > maxX && p3.x > maxX) ||
                (p1.y < minY && p2.y < minY && p3.y < minY) ||
                (p1.y > maxY && p2.y > maxY && p3.y > maxY))
                return false;

            p1 = ClampPoint(p1, minX, maxX, minY, maxY);
            p2 = ClampPoint(p2, minX, maxX, minY, maxY);
            p3 = ClampPoint(p3, minX, maxX, minY, maxY);
            return true;
        }

        private static (double x, double y) ClampPoint(
            (double x, double y) point,
            double minX,
            double maxX,
            double minY,
            double maxY)
        {
            return (
                Math.Clamp(point.x, minX, maxX),
                Math.Clamp(point.y, minY, maxY));
        }
    }
}
