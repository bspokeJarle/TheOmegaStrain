using Domain;

namespace _3DSpesificsUnitTests.Engine;

[TestClass]
public class EngineGroundShadowProjectionMathTests
{
    private static readonly GroundShadowProjectionOptions Options = new()
    {
        ShadowSize = 6f,
        BaseProjectedScale = 1f,
        MinProjectedScale = 0.3f,
        AltitudeShrinkFactor = 0.003f,
        AltitudeProjection = 0.15f,
        MaxAltitudeForProjection = 120f,
        ShadowLift = 0f,
        ShadowSlopeX = -0.15f,
        ShadowSlopeY = -0.55f,
        SurfaceTiltDegrees = 70f
    };

    [TestMethod]
    public void ProjectTriangleShadow_OnGroundAnchorsAtGroundPoint()
    {
        var shadow = GroundShadowProjectionMath.ProjectTriangleShadow(
            particleScreenY: 500f,
            groundScreenY: 500f,
            groundLocalX: 20f,
            groundLocalY: 0f,
            groundLocalZ: 10f,
            Options);

        Assert.AreEqual(20f, (shadow.Vertex1.x + shadow.Vertex2.x) / 2f, 0.001f);
        Assert.AreEqual(10f, shadow.Vertex1.z, 0.001f);
        Assert.AreEqual(1f, shadow.Scale, 0.001f);
    }

    [TestMethod]
    public void ProjectTriangleShadow_ClampsHighAltitudeProjectionButKeepsScaleFloor()
    {
        var high = GroundShadowProjectionMath.ProjectTriangleShadow(
            particleScreenY: -500f,
            groundScreenY: 500f,
            groundLocalX: 0f,
            groundLocalY: 0f,
            groundLocalZ: 0f,
            Options);
        var capped = GroundShadowProjectionMath.ProjectTriangleShadow(
            particleScreenY: 380f,
            groundScreenY: 500f,
            groundLocalX: 0f,
            groundLocalY: 0f,
            groundLocalZ: 0f,
            Options);

        Assert.AreEqual(GetCenterX(capped), GetCenterX(high), 0.001f);
        Assert.AreEqual(capped.Vertex1.y, high.Vertex1.y, 0.001f);
        Assert.AreEqual(Options.MaxAltitudeForProjection, high.ClampedAltitude, 0.001f);
        Assert.AreEqual(Options.MinProjectedScale, high.Scale, 0.001f);
    }

    private static float GetCenterX(GroundShadowProjectionResult projection)
    {
        return (projection.Vertex1.x + projection.Vertex2.x) / 2f;
    }
}
