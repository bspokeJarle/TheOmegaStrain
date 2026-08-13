using System.Collections.Generic;

namespace Domain
{
    public interface IProjectedTriangleRenderer<TTriangle> where TTriangle : IProjectedTriangle
    {
        int GetRenderingTriangleCount();
        void RenderTriangles(List<TTriangle> projectedTriangles);
    }
}
