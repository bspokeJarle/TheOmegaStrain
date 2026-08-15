using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class PalmControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.DeltaTime = 0.1f;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.DeltaTime = 0f;
    }

    [TestMethod]
    public void MoveObject_WindAnimation_AnimatesOnlyPalmLeavesAndKeepsObjectRotationStable()
    {
        var palm = PalmTree.CreateLargePalm(null!);
        palm.ObjectId = 11;
        palm.IsOnScreen = true;
        var controls = new LargePalmControls();

        var baseLeaves = CloneTriangles(GetPart(palm, "LargePalmLeaves").Triangles);
        var baseTrunk = CloneTriangles(GetPart(palm, "LargePalmTrunk").Triangles);
        var baseCrown = CloneTriangles(GetPart(palm, "LargePalmCrownCore").Triangles);
        var baseFruit = CloneTriangles(GetPart(palm, "LargePalmFruit").Triangles);
        var baseShadow = CloneTriangles(GetPart(palm, "Shadow").Triangles);

        controls.MoveObject(palm, null, null);

        Assert.AreEqual(WorldViewSetup.SurfaceFacingObjectPitchDegrees, palm.Rotation!.x, 0.001f, "Palm control should keep the surface-facing object rotation.");
        Assert.AreEqual(0f, palm.Rotation.y, 0.001f, "Palm control should not yaw the whole object.");
        Assert.AreEqual(0f, palm.Rotation.z, 0.001f, "Palm control should not spin the whole object.");

        Assert.IsTrue(
            MaxSpatialDisplacement(baseLeaves, GetPart(palm, "LargePalmLeaves").Triangles) > 0.5f,
            "Palm leaves should visibly sway.");

        AssertTrianglesEqual(baseTrunk, GetPart(palm, "LargePalmTrunk").Triangles, "Wind animation should not touch the trunk.");
        AssertTrianglesEqual(baseCrown, GetPart(palm, "LargePalmCrownCore").Triangles, "Wind animation should not touch the crown core.");
        AssertTrianglesEqual(baseFruit, GetPart(palm, "LargePalmFruit").Triangles, "Wind animation should not touch the fruit.");
        AssertTrianglesEqual(baseShadow, GetPart(palm, "Shadow").Triangles, "Wind animation should not touch the shadow geometry.");
    }

    [TestMethod]
    public void MoveObject_WindAnimation_RebuildsPalmLeavesFromOriginalGeometryEachFrame()
    {
        var palm = PalmTree.CreateLargePalm(null!);
        palm.ObjectId = 12;
        palm.IsOnScreen = true;
        var controls = new LargePalmControls();
        var baseLeaves = CloneTriangles(GetPart(palm, "LargePalmLeaves").Triangles);

        for (int i = 0; i < 80; i++)
            controls.MoveObject(palm, null, null);

        float displacement = MaxSpatialDisplacement(baseLeaves, GetPart(palm, "LargePalmLeaves").Triangles);

        Assert.IsTrue(displacement <= 6.6f, "Palm leaf wind animation should stay bounded instead of accumulating geometry drift.");
    }

    [TestMethod]
    public void MoveObject_WindAnimation_LargeAndSmallPalmsMoveOutOfSync()
    {
        var largePalm = PalmTree.CreateLargePalm(null!);
        var smallPalm = PalmTree.CreateSmallPalm(null!);
        largePalm.ObjectId = 21;
        smallPalm.ObjectId = 22;
        largePalm.IsOnScreen = true;
        smallPalm.IsOnScreen = true;

        var largeBaseLeaves = CloneTriangles(GetPart(largePalm, "LargePalmLeaves").Triangles);
        var smallBaseLeaves = CloneTriangles(GetPart(smallPalm, "SmallPalmLeaves").Triangles);

        var largeControls = new LargePalmControls();
        var smallControls = new SmallPalmControls();

        largeControls.MoveObject(largePalm, null, null);
        smallControls.MoveObject(smallPalm, null, null);

        float largeDisplacement = MaxSpatialDisplacement(largeBaseLeaves, GetPart(largePalm, "LargePalmLeaves").Triangles);
        float smallDisplacement = MaxSpatialDisplacement(smallBaseLeaves, GetPart(smallPalm, "SmallPalmLeaves").Triangles);

        Assert.IsTrue(largeDisplacement > 0.5f, "Large palm leaves should sway.");
        Assert.IsTrue(smallDisplacement > 0.35f, "Small palm leaves should sway.");
        Assert.AreNotEqual(largeDisplacement, smallDisplacement, 0.05f, "Large and small palms should not animate in lockstep.");
    }

    [TestMethod]
    public void MoveObject_WindAnimation_DoesNotAnimateWhenPalmIsOffscreen()
    {
        var palm = PalmTree.CreateLargePalm(null!);
        palm.ObjectId = 31;
        palm.IsOnScreen = false;
        var controls = new LargePalmControls();
        var baseLeaves = CloneTriangles(GetPart(palm, "LargePalmLeaves").Triangles);

        controls.MoveObject(palm, null, null);

        Assert.AreEqual(WorldViewSetup.SurfaceFacingObjectPitchDegrees, palm.Rotation!.x, 0.001f, "Palm control should still keep the normal object rotation.");
        AssertTrianglesEqual(baseLeaves, GetPart(palm, "LargePalmLeaves").Triangles, "Offscreen palms should not spend work animating leaves.");
    }

    private static I3dObjectPart GetPart(I3dObject palm, string partName)
    {
        var part = palm.ObjectParts.SingleOrDefault(part => part.PartName == partName);
        Assert.IsNotNull(part, $"Expected palm part '{partName}' to exist.");
        return part;
    }

    private static List<ITriangleMeshWithColor> CloneTriangles(List<ITriangleMeshWithColor> source)
    {
        var clone = new List<ITriangleMeshWithColor>(source.Count);
        foreach (var triangle in source)
        {
            clone.Add(new TriangleMeshWithColor
            {
                Color = triangle.Color,
                noHidden = triangle.noHidden,
                landBasedPosition = triangle.landBasedPosition,
                angle = triangle.angle,
                vert1 = CopyVertex(triangle.vert1),
                vert2 = CopyVertex(triangle.vert2),
                vert3 = CopyVertex(triangle.vert3),
                normal1 = CopyVertex(triangle.normal1),
                normal2 = CopyVertex(triangle.normal2),
                normal3 = CopyVertex(triangle.normal3)
            });
        }

        return clone;
    }

    private static float MaxSpatialDisplacement(
        List<ITriangleMeshWithColor> expected,
        List<ITriangleMeshWithColor> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count, "Animated leaves should preserve the original triangle count.");

        float maxDisplacement = 0f;
        for (int i = 0; i < expected.Count; i++)
        {
            maxDisplacement = MathF.Max(maxDisplacement, SpatialDistance(expected[i].vert1, actual[i].vert1));
            maxDisplacement = MathF.Max(maxDisplacement, SpatialDistance(expected[i].vert2, actual[i].vert2));
            maxDisplacement = MathF.Max(maxDisplacement, SpatialDistance(expected[i].vert3, actual[i].vert3));
        }

        return maxDisplacement;
    }

    private static float SpatialDistance(IVector3 expected, IVector3 actual)
    {
        float dx = actual.x - expected.x;
        float dy = actual.y - expected.y;
        float dz = actual.z - expected.z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static Vector3 CopyVertex(IVector3 vertex)
    {
        return new Vector3 { x = vertex.x, y = vertex.y, z = vertex.z };
    }

    private static void AssertTrianglesEqual(
        List<ITriangleMeshWithColor> expected,
        List<ITriangleMeshWithColor> actual,
        string message)
    {
        Assert.AreEqual(expected.Count, actual.Count, message);

        for (int i = 0; i < expected.Count; i++)
        {
            AssertSameVertex(expected[i].vert1, actual[i].vert1, message);
            AssertSameVertex(expected[i].vert2, actual[i].vert2, message);
            AssertSameVertex(expected[i].vert3, actual[i].vert3, message);
        }
    }

    private static void AssertSameVertex(IVector3 expected, IVector3 actual, string message)
    {
        Assert.AreEqual(expected.x, actual.x, 0.001f, message);
        Assert.AreEqual(expected.y, actual.y, 0.001f, message);
        Assert.AreEqual(expected.z, actual.z, 0.001f, message);
    }
}
