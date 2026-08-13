using Domain;

namespace _3DSpesificsUnitTests.Engine;

[TestClass]
public class EngineObjectShadowProjectionMathTests
{
    [TestMethod]
    public void ProjectModelTriangleShadow_AnchorsGroundVerticesAndStretchesTallVertices()
    {
        var triangle = new EngineTriangleMeshWithColor
        {
            vert1 = new EngineVector3(0f, 0f, 0f),
            vert2 = new EngineVector3(10f, 0f, 0f),
            vert3 = new EngineVector3(0f, 0f, 20f)
        };
        var options = new ObjectShadowProjectionOptions
        {
            ShadowBaseX = 100f,
            ShadowBaseY = 200f,
            ShadowBaseZ = 50f,
            Scale = 1f,
            ShadowSlopeX = -0.15f,
            ShadowSlopeY = -0.55f,
            VertexStretchBoost = 1.2f,
            SurfaceTiltDegrees = 70f
        };

        var projected = ObjectShadowProjectionMath.ProjectModelTriangleShadow(triangle, options);

        Assert.AreEqual(100f, projected.Vertex1.x, 0.001f);
        Assert.AreEqual(200f, projected.Vertex1.y, 0.001f);
        Assert.AreEqual(50f, projected.Vertex1.z, 0.001f);
        Assert.AreEqual(110f, projected.Vertex2.x, 0.001f);

        Assert.IsTrue(projected.Vertex3.x < projected.Vertex1.x,
            "A raised vertex should stretch along the negative X light slope.");
        Assert.IsTrue(projected.Vertex3.y < projected.Vertex1.y,
            "A raised vertex should move up-screen after the surface tilt is baked in.");
        Assert.IsTrue(projected.Vertex3.z < projected.Vertex1.z,
            "A raised vertex should move along the tilted ground plane, not stay vertical.");
    }

    [TestMethod]
    public void ProjectModelTriangleShadow_AppliesBaseOffsetAndScale()
    {
        var triangle = new EngineTriangleMeshWithColor
        {
            vert1 = new EngineVector3(1f, 2f, 0f),
            vert2 = new EngineVector3(2f, 2f, 0f),
            vert3 = new EngineVector3(1f, 3f, 0f)
        };
        var options = new ObjectShadowProjectionOptions
        {
            ShadowBaseX = 10f,
            ShadowBaseY = 20f,
            ShadowBaseZ = 30f,
            ShadowOffsetX = -2f,
            ShadowOffsetY = 4f,
            ShadowOffsetZ = 6f,
            Scale = 2f,
            SurfaceTiltDegrees = 0f
        };

        var projected = ObjectShadowProjectionMath.ProjectModelTriangleShadow(triangle, options);

        Assert.AreEqual(10f, projected.Vertex1.x, 0.001f);
        Assert.AreEqual(28f, projected.Vertex1.y, 0.001f);
        Assert.AreEqual(36f, projected.Vertex1.z, 0.001f);
    }

    private sealed class EngineTriangleMeshWithColor : EngineTriangleMesh, ITriangleMeshWithColor
    {
        public string? Color { get; set; }
    }
}
