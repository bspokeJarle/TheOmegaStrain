using _3dRotations.World.Objects;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls;
using System.Reflection;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class TreeControlsTests
{
    [TestMethod]
    public void MoveObject_WindAnimation_AnimatesOnlyTreePartsAndKeepsBaseAnchored()
    {
        var tree = CreateTreeObject();
        var controls = new TreeControls();

        var baseTrunkBottom = CopyVertex(GetPart(tree, "TreeTrunk").Triangles[0].vert1);
        var baseTrunkTop = CopyVertex(GetPart(tree, "TreeTrunk").Triangles[0].vert2);
        var baseFoliageTop = CopyVertex(GetPart(tree, "TreeFoliage").Triangles[0].vert2);
        var baseShadowVertex = CopyVertex(GetPart(tree, "Shadow").Triangles[0].vert2);

        controls.MoveObject(tree, null, null);

        var trunk = GetPart(tree, "TreeTrunk");
        var foliage = GetPart(tree, "TreeFoliage");
        var shadow = GetPart(tree, "Shadow");

        Assert.AreEqual(WorldViewSetup.SurfaceFacingObjectPitchDegrees, tree.Rotation!.x, 0.001f, "Tree control should keep the normal tree rotation.");
        AssertSameVertex(baseTrunkBottom, trunk.Triangles[0].vert1, "The trunk base should stay pinned to the ground.");
        AssertSameVertex(baseShadowVertex, shadow.Triangles[0].vert2, "Wind animation should not touch the shadow part.");

        float trunkTopMove = PlanarDistance(baseTrunkTop, trunk.Triangles[0].vert2);
        float foliageTopMove = PlanarDistance(baseFoliageTop, foliage.Triangles[0].vert2);

        Assert.IsTrue(trunkTopMove > 0f, "The trunk top should move slightly.");
        Assert.IsTrue(foliageTopMove > trunkTopMove + 0.25f, "The foliage should sway more than the trunk.");
    }

    [TestMethod]
    public void MoveObject_WindAnimation_RebuildsFromOriginalGeometryEachFrame()
    {
        var tree = CreateTreeObject();
        var controls = new TreeControls();
        var baseFoliageTop = CopyVertex(GetPart(tree, "TreeFoliage").Triangles[0].vert2);

        for (int i = 0; i < 60; i++)
        {
            SetPrivateField(controls, "_lastFrameTime", DateTime.Now.AddSeconds(-0.1));
            controls.MoveObject(tree, null, null);
        }

        var foliageTop = GetPart(tree, "TreeFoliage").Triangles[0].vert2;
        float displacement = PlanarDistance(baseFoliageTop, foliageTop);

        Assert.IsTrue(displacement <= 8.2f, "Wind animation should stay bounded instead of accumulating geometry drift.");
    }

    [TestMethod]
    public void MoveObject_WindAnimation_IsVisibleOnRealTreeGeometry()
    {
        var tree = Tree.CreateTree(null!);
        tree.ObjectId = 0;
        var controls = new TreeControls();
        var baseFoliage = CloneTriangles(GetPart(tree, "TreeFoliage").Triangles);

        controls.MoveObject(tree, null, null);
        for (int i = 0; i < 10; i++)
        {
            SetPrivateField(controls, "_lastFrameTime", DateTime.Now.AddSeconds(-0.1));
            controls.MoveObject(tree, null, null);
        }

        float maxDisplacement = MaxPlanarDisplacement(baseFoliage, GetPart(tree, "TreeFoliage").Triangles);

        Assert.IsTrue(maxDisplacement >= 4f, "The real tree foliage should sway enough to be visible in-game.");
    }

    [TestMethod]
    public void CreateLeafTree_UsesTreeWindPartsAndLowPolyLeafPalette()
    {
        var leafTree = LeafTree.CreateLeafTree(null!);
        var trunk = GetPart(leafTree, "TreeTrunk");
        var foliage = GetPart(leafTree, "TreeFoliage");

        Assert.IsTrue(trunk.Triangles.Count > 20, "LeafTree should keep trunk geometry and add branch geometry.");
        Assert.IsTrue(foliage.Triangles.Count > 20, "LeafTree should use several simple leaf triangles instead of one canopy blob.");
        Assert.IsTrue(foliage.Triangles.All(triangle => triangle.noHidden == true), "Low-poly leaf triangles should be visible from both sides.");
        Assert.IsTrue(foliage.Triangles.All(triangle => LeafTree.LeafColors.Contains(triangle.Color)), "LeafTree foliage should use the shared leaf palette.");
        Assert.IsTrue(leafTree.CrashBoxes.Count >= 3, "LeafTree should have crashboxes like other landbased tree objects.");
    }

    private static _3dObject CreateTreeObject()
    {
        return new _3dObject
        {
            ObjectId = 7,
            ObjectName = "Tree",
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "TreeTrunk",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "8B4513",
                            vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 2f, y = 0f, z = 15f },
                            vert3 = new Vector3 { x = 0f, y = 2f, z = 15f }
                        }
                    }
                },
                new _3dObjectPart
                {
                    PartName = "TreeFoliage",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "008800",
                            vert1 = new Vector3 { x = 0f, y = 0f, z = 20f },
                            vert2 = new Vector3 { x = 3f, y = 0f, z = 45f },
                            vert3 = new Vector3 { x = 0f, y = 3f, z = 45f }
                        }
                    }
                },
                new _3dObjectPart
                {
                    PartName = "Shadow",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "000000",
                            vert1 = new Vector3 { x = -1f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 1f, y = 0f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = 2f, z = 0f }
                        }
                    }
                }
            }
        };
    }

    private static I3dObjectPart GetPart(I3dObject tree, string partName)
    {
        var part = tree.ObjectParts.SingleOrDefault(part => part.PartName == partName);
        Assert.IsNotNull(part, $"Expected tree part '{partName}' to exist.");
        return part;
    }

    private static Vector3 CopyVertex(IVector3 vertex)
    {
        return new Vector3 { x = vertex.x, y = vertex.y, z = vertex.z };
    }

    private static float PlanarDistance(IVector3 expected, IVector3 actual)
    {
        float dx = actual.x - expected.x;
        float dy = actual.y - expected.y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static List<ITriangleMeshWithColor> CloneTriangles(List<ITriangleMeshWithColor> source)
    {
        var clone = new List<ITriangleMeshWithColor>(source.Count);
        foreach (var triangle in source)
        {
            clone.Add(new TriangleMeshWithColor
            {
                Color = triangle.Color,
                vert1 = CopyVertex(triangle.vert1),
                vert2 = CopyVertex(triangle.vert2),
                vert3 = CopyVertex(triangle.vert3)
            });
        }

        return clone;
    }

    private static float MaxPlanarDisplacement(
        List<ITriangleMeshWithColor> expected,
        List<ITriangleMeshWithColor> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count, "Animated foliage should preserve the original triangle count.");

        float maxDisplacement = 0f;
        for (int i = 0; i < expected.Count; i++)
        {
            maxDisplacement = MathF.Max(maxDisplacement, PlanarDistance(expected[i].vert1, actual[i].vert1));
            maxDisplacement = MathF.Max(maxDisplacement, PlanarDistance(expected[i].vert2, actual[i].vert2));
            maxDisplacement = MathF.Max(maxDisplacement, PlanarDistance(expected[i].vert3, actual[i].vert3));
        }

        return maxDisplacement;
    }

    private static void AssertSameVertex(IVector3 expected, IVector3 actual, string message)
    {
        Assert.AreEqual(expected.x, actual.x, 0.001f, message);
        Assert.AreEqual(expected.y, actual.y, 0.001f, message);
        Assert.AreEqual(expected.z, actual.z, 0.001f, message);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }
}
