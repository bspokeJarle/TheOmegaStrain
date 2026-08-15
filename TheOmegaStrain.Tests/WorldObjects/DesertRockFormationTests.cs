using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using GameAiAndControls.Controls;
using System.Linq;

namespace _3DSpesificsUnitTests.WorldObjects;

[TestClass]
public class DesertRockFormationTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState();
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void CreateDesertRockFormation_HasRockClusterPartsCollisionAndShadow()
    {
        var rocks = DesertRockFormation.CreateDesertRockFormation(null!);

        Assert.AreEqual("DesertRockFormation", rocks.ObjectName);
        Assert.IsTrue(rocks.HasShadow);
        Assert.IsInstanceOfType(rocks.Movement, typeof(DesertRockControls));
        Assert.IsTrue(rocks.CrashBoxes.Count >= 1);
        Assert.IsTrue(rocks.ObjectParts.Any(p => p.PartName == "DesertRockMain"));
        Assert.IsTrue(rocks.ObjectParts.Any(p => p.PartName == "DesertRockSideA"));
        Assert.IsTrue(rocks.ObjectParts.Any(p => p.PartName == "DesertRockSideB"));
        Assert.IsTrue(rocks.ObjectParts.Any(p => p.PartName == "DesertRockFront"));
        Assert.IsTrue(rocks.ObjectParts.Any(p => p.PartName == "Shadow"));
    }

    [TestMethod]
    public void CreateDesertRockFormation_RotatesWithoutChangingPartStructure()
    {
        var formations = Enumerable.Range(0, 8)
            .Select(_ => DesertRockFormation.CreateDesertRockFormation(null!))
            .ToList();

        var firstPartNames = formations[0].ObjectParts
            .Where(p => p.PartName != "Shadow")
            .Select(p => p.PartName)
            .OrderBy(name => name)
            .ToList();

        foreach (var formation in formations)
        {
            var partNames = formation.ObjectParts
                .Where(p => p.PartName != "Shadow")
                .Select(p => p.PartName)
                .OrderBy(name => name)
                .ToList();

            CollectionAssert.AreEqual(firstPartNames, partNames);
        }

        int distinctOrientations = formations
            .Select(f =>
            {
                var vertex = f.ObjectParts.Single(p => p.PartName == "DesertRockMain").Triangles[0].vert1;
                return (x: MathF.Round(vertex.x, 2), y: MathF.Round(vertex.y, 2));
            })
            .Distinct()
            .Count();

        Assert.IsTrue(distinctOrientations >= 4, "Rock formations should keep structure and vary by rotation.");
    }
}
