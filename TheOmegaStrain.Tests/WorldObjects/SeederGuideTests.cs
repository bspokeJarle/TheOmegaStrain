using _3dRotations.World.Objects;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.WorldObjects;

[TestClass]
public class SeederGuideTests
{
    [TestMethod]
    public void ParticleGuides_AreCenteredUnderSeederWithSameGuideDistance()
    {
        var start = GetCentroid(Seeder.ParticlesStartGuide()![0]);
        var guide = GetCentroid(Seeder.ParticlesDirectionGuide()![0]);

        Assert.AreEqual(0f, start.x, 0.001f, "Seeder particle start guide should be centered on local X.");
        Assert.AreEqual(0f, start.y, 0.001f, "Seeder particle start guide should be centered on local Y.");
        Assert.AreEqual(-13f, start.z, 0.001f, "Seeder particle start guide should sit at the bottom center module.");

        Assert.AreEqual(start.x, guide.x, 0.001f, "Seeder particle end guide should stay centered on local X.");
        Assert.AreEqual(start.y, guide.y, 0.001f, "Seeder particle end guide should stay centered on local Y.");
        Assert.AreEqual(100f, MathF.Abs(guide.z - start.z), 0.001f, "Seeder particle guide distance should stay unchanged.");
    }

    [TestMethod]
    public void ParticleGuides_ArePointAnchorsOnSeederCenterLine()
    {
        var start = Seeder.ParticlesStartGuide()![0];
        var guide = Seeder.ParticlesDirectionGuide()![0];

        AssertPointAnchor(start, -13f);
        AssertPointAnchor(guide, -113f);
    }

    [TestMethod]
    public void CreatedSeeder_StartGuideIsAtScaledVisualBottomCenter()
    {
        var seeder = Seeder.CreateSeeder(null!);
        var start = seeder.ObjectParts.Single(p => p.PartName == "SeederParticlesStartGuide").Triangles![0];
        float visibleBottomZ = seeder.ObjectParts
            .Where(p => p.IsVisible && p.Triangles != null)
            .SelectMany(p => p.Triangles!)
            .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 })
            .Min(v => v.z);

        AssertPointAnchor(start, visibleBottomZ);
    }

    private static void AssertPointAnchor(ITriangleMeshWithColor triangle, float expectedZ)
    {
        AssertVertex(triangle.vert1, expectedZ);
        AssertVertex(triangle.vert2, expectedZ);
        AssertVertex(triangle.vert3, expectedZ);
    }

    private static void AssertVertex(IVector3 vertex, float expectedZ)
    {
        Assert.AreEqual(0f, vertex.x, 0.001f, "Seeder particle guide vertex should be centered on local X.");
        Assert.AreEqual(0f, vertex.y, 0.001f, "Seeder particle guide vertex should be centered on local Y.");
        Assert.AreEqual(expectedZ, vertex.z, 0.001f, "Seeder particle guide vertex should sit on the expected local Z.");
    }

    private static Vector3 GetCentroid(ITriangleMeshWithColor triangle)
    {
        return new Vector3
        {
            x = (triangle.vert1.x + triangle.vert2.x + triangle.vert3.x) / 3f,
            y = (triangle.vert1.y + triangle.vert2.y + triangle.vert3.y) / 3f,
            z = (triangle.vert1.z + triangle.vert2.z + triangle.vert3.z) / 3f
        };
    }
}
