using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Engine;

[TestClass]
public class EngineTransformMathTests
{
    [TestMethod]
    public void GroundProjectionMath_InterpolatesYInsideTriangle()
    {
        var triangle = new TriangleMeshWithColor
        {
            vert1 = new Vector3(0f, 10f, 0f),
            vert2 = new Vector3(10f, 20f, 0f),
            vert3 = new Vector3(0f, 30f, 10f)
        };

        bool found = GroundProjectionMath.TryGetSurfaceGroundPoint(
            new List<ITriangleMeshWithColor> { triangle },
            2.5f,
            2.5f,
            out float groundX,
            out float groundY,
            out float groundZ);

        Assert.IsTrue(found);
        Assert.AreEqual(2.5f, groundX, 0.001f);
        Assert.AreEqual(17.5f, groundY, 0.001f);
        Assert.AreEqual(2.5f, groundZ, 0.001f);
    }

    [TestMethod]
    public void CrashBoxTransform_UsesCalculatedCrashOffsetBeforeObjectOffset()
    {
        var obj = new Engine3dObject
        {
            ObjectId = 1,
            ObjectName = "Test",
            ObjectOffsets = new EngineVector3(10f, 20f, 30f),
            CalculatedCrashOffset = new EngineVector3(1f, 2f, 3f)
        };

        var offset = CrashBoxTransform.GetEffectiveCrashOffset(
            obj,
            static (x, y, z) => new EngineVector3(x, y, z));

        Assert.AreEqual(1f, offset.x, 0.001f);
        Assert.AreEqual(2f, offset.y, 0.001f);
        Assert.AreEqual(3f, offset.z, 0.001f);
    }

    [TestMethod]
    public void WorldPositionMath_ReturnsNullLocalPositionForOriginScreenObject()
    {
        var obj = new Engine3dObject
        {
            ObjectId = 1,
            ObjectName = "ScreenObject",
            WorldPosition = new EngineVector3()
        };

        var local = WorldPositionMath.GetLocalWorldPosition(
            obj,
            new EngineVector3(100f, 200f, 300f),
            static (x, y, z) => new EngineVector3(x, y, z));

        Assert.IsNull(local);
    }

    [TestMethod]
    public void WorldPositionMath_CalculatesAudioPositionFromLocalWorldAndOffsets()
    {
        var obj = new Engine3dObject
        {
            ObjectId = 1,
            ObjectName = "AudioObject",
            ObjectOffsets = new EngineVector3(5f, 6f, 7f)
        };

        var audio = WorldPositionMath.GetAudioPosition(
            obj,
            new EngineVector3(10f, 20f, 30f),
            static (x, y, z) => new EngineVector3(x, y, z));

        Assert.AreEqual(-5f, audio.x, 0.001f);
        Assert.AreEqual(-14f, audio.y, 0.001f);
        Assert.AreEqual(37f, audio.z, 0.001f);
    }
}
