using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using GameAiAndControls.Controls;
using System.Linq;

namespace _3DSpesificsUnitTests.WorldObjects;

[TestClass]
public class CactusGeometryTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState();
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void CreateCactus_HasDesertSilhouettePartsAndCollision()
    {
        var cactus = Cactus.CreateCactus(null!);

        Assert.IsTrue(cactus.HasShadow);
        Assert.IsInstanceOfType(cactus.Movement, typeof(CactusControls));
        Assert.IsTrue(cactus.CrashBoxes.Count >= 3);
        Assert.IsTrue(cactus.ObjectParts.Any(p => p.PartName == "CactusBody"));
        Assert.IsTrue(cactus.ObjectParts.Any(p => p.PartName == "CactusLeftArm"));
        Assert.IsTrue(cactus.ObjectParts.Any(p => p.PartName == "CactusRightArm"));
        Assert.IsTrue(cactus.ObjectParts.Any(p => p.PartName == "Shadow"));
    }

    [TestMethod]
    public void CreateCactus_KeepsOriginalShapeAndOnlyRotates()
    {
        var cacti = Enumerable.Range(0, 8)
            .Select(_ => Cactus.CreateCactus(null!))
            .ToList();

        foreach (var cactus in cacti)
        {
            var visiblePartNames = cactus.ObjectParts
                .Where(p => p.PartName != "Shadow")
                .Select(p => p.PartName)
                .OrderBy(name => name)
                .ToList();

            Assert.IsTrue(visiblePartNames.Contains("CactusBloom"));
            Assert.IsTrue(visiblePartNames.Contains("CactusBody"));
            Assert.IsTrue(visiblePartNames.Contains("CactusLeftArm"));
            Assert.IsTrue(visiblePartNames.Contains("CactusRightArm"));
            CollectionAssert.AreEqual(
                new[] { "CactusBloom", "CactusBody", "CactusLeftArm", "CactusRightArm" },
                visiblePartNames,
                "Cactus should keep exactly the original readable cactus shape.");
        }

        var bodyFirstVertices = cacti
            .Select(c =>
            {
                var vertex = c.ObjectParts.Single(p => p.PartName == "CactusBody").Triangles[0].vert1;
                return (x: MathF.Round(vertex.x, 2), y: MathF.Round(vertex.y, 2));
            })
            .Distinct()
            .Count();

        Assert.IsTrue(bodyFirstVertices >= 4, "Cactus should keep the original mesh and vary visibly by rotation.");
        AssertSameMeshStructure(cacti[0], cacti[1]);
        AssertSameMeshStructure(cacti[0], cacti[2]);
    }

    [TestMethod]
    public void CactusControls_PreservesObjectRotation()
    {
        var cactus = Cactus.CreateCactus(null!);
        cactus.Rotation.z = 18f;

        cactus.Movement!.MoveObject(cactus, null, null);

        Assert.AreEqual(WorldViewSetup.SurfaceFacingObjectPitchDegrees, cactus.Rotation.x);
        Assert.AreEqual(0f, cactus.Rotation.y);
        Assert.AreEqual(18f, cactus.Rotation.z);
    }

    [TestMethod]
    public void CreateCactus_RotationDoesNotReuseMutableVertexInstances()
    {
        var cactus = Cactus.CreateCactus(null!);
        var vertices = cactus.ObjectParts
            .Where(p => p.PartName != "Shadow")
            .SelectMany(p => p.Triangles)
            .SelectMany(t => new[] { t.vert1, t.vert2, t.vert3 })
            .ToList();

        for (int i = 0; i < vertices.Count; i++)
        {
            for (int j = i + 1; j < vertices.Count; j++)
            {
                Assert.IsFalse(
                    ReferenceEquals(vertices[i], vertices[j]),
                    "Cactus rotation should not reuse mutable vertex instances across triangle slots.");
            }
        }
    }

    private static void AssertSameMeshStructure(Domain.I3dObject expected, Domain.I3dObject actual)
    {
        foreach (var expectedPart in expected.ObjectParts.Where(p => p.PartName != "Shadow"))
        {
            var actualPart = actual.ObjectParts.Single(p => p.PartName == expectedPart.PartName);
            Assert.AreEqual(expectedPart.Triangles.Count, actualPart.Triangles.Count, expectedPart.PartName);

            for (int i = 0; i < expectedPart.Triangles.Count; i++)
            {
                Assert.AreEqual(expectedPart.Triangles[i].Color, actualPart.Triangles[i].Color, expectedPart.PartName);
            }
        }
    }
}
