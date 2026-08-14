using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public interface ISurfaceGeometryCache
    {
        List<ITriangleMeshWithColor> RotatedSurfaceTriangles { get; set; }
        Dictionary<long, ITriangleMeshWithColor> RotatedSurfaceTriangleByLandId { get; set; }
        HashSet<long?> LandBasedIds { get; set; }
    }
}
