using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public interface IProjectedTriangleRenderer<TTriangle> where TTriangle : IProjectedTriangle
    {
        int GetRenderingTriangleCount();
        void RenderTriangles(List<TTriangle> projectedTriangles);
    }
}
