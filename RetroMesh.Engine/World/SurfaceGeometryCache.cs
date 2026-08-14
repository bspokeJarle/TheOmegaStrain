using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public static class SurfaceGeometryCache
    {
        public static void Update(
            ISurfaceGeometryCache? cache,
            List<ITriangleMeshWithColor> rotatedSurfaceTriangles)
        {
            if (cache == null)
                return;

            cache.RotatedSurfaceTriangles = rotatedSurfaceTriangles;

            var landBasedIds = cache.LandBasedIds;
            landBasedIds.Clear();

            var triangleByLandId = cache.RotatedSurfaceTriangleByLandId;
            triangleByLandId.Clear();

            foreach (var triangle in rotatedSurfaceTriangles)
            {
                var landBasedPosition = triangle.landBasedPosition;
                landBasedIds.Add(landBasedPosition);

                if (landBasedPosition.HasValue)
                    triangleByLandId[landBasedPosition.Value] = triangle;
            }
        }
    }
}
