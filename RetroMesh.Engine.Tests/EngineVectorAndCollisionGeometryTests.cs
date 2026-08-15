using System.Collections.Generic;

namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineVectorAndCollisionGeometryTests
{
    [TestMethod]
    public void LerpColorHex_ClampsAndInterpolatesChannels()
    {
        Assert.AreEqual("7F7F7F", ColorMath.LerpColorHex("000000", "FFFFFF", 0.5f));
        Assert.AreEqual("000000", ColorMath.LerpColorHex("000000", "FFFFFF", -1f));
        Assert.AreEqual("FFFFFF", ColorMath.LerpColorHex("000000", "FFFFFF", 2f));
    }

    [TestMethod]
    public void LerpColorHex_CanUseLowercaseRoundedChannelsForParticleColors()
    {
        Assert.AreEqual(
            "808080",
            ColorMath.LerpColorHex("000000", "ffffff", 0.5f, lowerCase: true, roundChannels: true));
    }

    [TestMethod]
    public void TryProjectVertex_RawViewportValuesMatchPerspectiveFormula()
    {
        bool projected = ProjectionMath.TryProjectVertex(
            new EngineVector3(10f, 20f, 50f),
            objectScreenX: 100,
            objectScreenY: 200,
            objectScreenZ: 300,
            perspectiveAdjustment: 1000,
            objectZoom: 2,
            out var screenPoint);

        Assert.IsTrue(projected);
        Assert.AreEqual(116, screenPoint.x, 0.001);
        Assert.AreEqual(232, screenPoint.y, 0.001);
    }

    [TestMethod]
    public void RotateAroundAxis_RotatesPointAroundZAxis()
    {
        var result = VectorMath.RotateAroundAxis(
            new EngineVector3(1f, 0f, 0f),
            new EngineVector3(0f, 0f, 1f),
            90f,
            new EngineVector3());

        Assert.AreEqual(0f, result.x, 0.001f);
        Assert.AreEqual(1f, result.y, 0.001f);
        Assert.AreEqual(0f, result.z, 0.001f);
    }

    [TestMethod]
    public void CalculateTriangleGeometryCenter_AveragesTriangleCenters()
    {
        var obj = new Engine3dObject
        {
            ObjectId = 1,
            ObjectParts =
            {
                new Engine3dObjectPart
                {
                    Triangles =
                    {
                        CreateTriangle(
                            new EngineVector3(0f, 0f, 0f),
                            new EngineVector3(3f, 0f, 0f),
                            new EngineVector3(0f, 3f, 0f)),
                        CreateTriangle(
                            new EngineVector3(6f, 6f, 6f),
                            new EngineVector3(9f, 6f, 6f),
                            new EngineVector3(6f, 9f, 6f))
                    }
                }
            }
        };

        var center = VectorMath.CalculateTriangleGeometryCenter(obj);

        Assert.AreEqual(4f, center.x, 0.001f);
        Assert.AreEqual(4f, center.y, 0.001f);
        Assert.AreEqual(3f, center.z, 0.001f);
    }

    [TestMethod]
    public void GetObjectCrashCenterWorldPosition_CanIncludeOrIgnoreObjectOffsets()
    {
        var obj = CreateObjectWithCrashBox();

        var withOffsets = ObjectCollisionGeometry.GetObjectCrashCenterWorldPosition(
            obj,
            includeObjectOffsets: true);
        var withoutOffsets = ObjectCollisionGeometry.GetObjectCrashCenterWorldPosition(
            obj,
            includeObjectOffsets: false);

        Assert.AreEqual(106f, withOffsets.x, 0.001f);
        Assert.AreEqual(107f, withOffsets.y, 0.001f);
        Assert.AreEqual(108f, withOffsets.z, 0.001f);

        Assert.AreEqual(105f, withoutOffsets.x, 0.001f);
        Assert.AreEqual(105f, withoutOffsets.y, 0.001f);
        Assert.AreEqual(105f, withoutOffsets.z, 0.001f);
    }

    [TestMethod]
    public void GetApproximateCrashRadius_UsesRotatedCrashBoxGeometry()
    {
        var obj = CreateObjectWithCrashBox();

        float radius = ObjectCollisionGeometry.GetApproximateCrashRadius(obj);

        Assert.AreEqual(MathF.Sqrt(75f), radius, 0.001f);
    }

    private static Engine3dObject CreateObjectWithCrashBox()
    {
        return new Engine3dObject
        {
            ObjectId = 1,
            WorldPosition = new EngineVector3(100f, 100f, 100f),
            ObjectOffsets = new EngineVector3(1f, 2f, 3f),
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new EngineVector3(0f, 0f, 0f),
                    new EngineVector3(10f, 10f, 10f)
                }
            }
        };
    }

    private static TestTriangleWithColor CreateTriangle(
        IVector3 vert1,
        IVector3 vert2,
        IVector3 vert3)
    {
        return new TestTriangleWithColor
        {
            vert1 = vert1,
            vert2 = vert2,
            vert3 = vert3,
            Color = "ffffff"
        };
    }

    private sealed class TestTriangleWithColor : EngineTriangleMesh, ITriangleMeshWithColor
    {
        public string Color { get; set; } = string.Empty;
    }
}
