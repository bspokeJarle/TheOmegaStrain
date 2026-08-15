using _3dRotations.World.Objects;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.WorldObjects;

[TestClass]
public class BambooHutGeometryTests
{
    [TestMethod]
    public void CreateBambooHut_UsesModerateTriangleCount()
    {
        var hut = BambooHut.CreateBambooHut(parentSurface: null!);

        int triangleCount = CountTriangles(hut);

        Assert.IsTrue(triangleCount <= 170,
            $"BambooHut should stay reasonably light for map-wide placement. Actual triangles: {triangleCount}.");
    }

    [TestMethod]
    public void CreateBambooHut_IsScaledUpForSceneReadability()
    {
        var hut = BambooHut.CreateBambooHut(parentSurface: null!);

        var bounds = GetVisibleBounds(hut);

        Assert.IsTrue(bounds.width >= 50f, $"Expected a slightly larger hut footprint. Width was {bounds.width:0.##}.");
        Assert.IsTrue(bounds.height >= 44f, $"Expected a slightly taller hut silhouette. Height was {bounds.height:0.##}.");
    }

    private static int CountTriangles(I3dObject hut)
    {
        int count = 0;
        foreach (var part in hut.ObjectParts)
        {
            count += part.Triangles?.Count ?? 0;
        }

        return count;
    }

    private static (float width, float height) GetVisibleBounds(I3dObject hut)
    {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;

        foreach (var part in hut.ObjectParts)
        {
            if (!part.IsVisible || part.Triangles == null)
                continue;

            foreach (var triangle in part.Triangles)
            {
                Include(triangle.vert1);
                Include(triangle.vert2);
                Include(triangle.vert3);
            }
        }

        return (maxX - minX, maxZ - minZ);

        void Include(IVector3 vertex)
        {
            minX = MathF.Min(minX, vertex.x);
            maxX = MathF.Max(maxX, vertex.x);
            minZ = MathF.Min(minZ, vertex.z);
            maxZ = MathF.Max(maxZ, vertex.z);
        }
    }
}
