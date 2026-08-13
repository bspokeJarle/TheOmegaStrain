using Domain;

namespace _3DSpesificsUnitTests.Engine;

[TestClass]
public class EngineObjectPlacementMathTests
{
    [TestMethod]
    public void GetObjectGeometricCenter_CanSnapToBottomYFootprint()
    {
        var obj = new Engine3dObject
        {
            ObjectId = 1,
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart
                {
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            vert1 = new EngineVector3(10f, -20f, 30f),
                            vert2 = new EngineVector3(30f, -20f, 30f),
                            vert3 = new EngineVector3(200f, 80f, 300f)
                        }
                    }
                }
            }
        };

        var anchor = ObjectPlacementMath.GetObjectGeometricCenter(
            obj,
            snapToBottomY: true,
            static (x, y, z) => new EngineVector3(x, y, z));

        Assert.AreEqual(20f, anchor.x, 0.001f);
        Assert.AreEqual(-20f, anchor.y, 0.001f);
        Assert.AreEqual(30f, anchor.z, 0.001f);
    }

    [TestMethod]
    public void TryGetRenderPosition_SurfaceAnchorCentersMeshAndCrashboxes()
    {
        var target = new EngineVector3(25f, 15f, 5f);
        var obj = new Engine3dObject
        {
            ObjectId = 2,
            ObjectOffsets = new EngineVector3(),
            WorldPosition = new EngineVector3(),
            UseSurfaceFootprintPivot = true,
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart
                {
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            vert1 = new EngineVector3(),
                            vert2 = new EngineVector3(10f, 0f, 0f),
                            vert3 = new EngineVector3(0f, -100f, 50f)
                        }
                    }
                }
            },
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new EngineVector3(-1f, -1f, -1f),
                    new EngineVector3(1f, 1f, 1f)
                }
            }
        };

        bool positioned = ObjectPlacementMath.TryGetRenderPosition(
            obj,
            localWorldPosition: null,
            surfaceAnchor: target,
            screenCenterX: 100,
            screenCenterY: 100,
            static (x, y, z) => new EngineVector3(x, y, z),
            out double x,
            out double y,
            out double z);

        Assert.IsTrue(positioned);
        Assert.AreEqual(100d, x, 0.001d);
        Assert.AreEqual(100d, y, 0.001d);
        Assert.AreEqual(0d, z, 0.001d);
        Assert.AreEqual(target.x, obj.ObjectParts[0].Triangles[0].vert1.x, 0.001f);
        Assert.AreEqual(target.y, obj.ObjectParts[0].Triangles[0].vert1.y, 0.001f);
        Assert.AreEqual(target.z, obj.ObjectParts[0].Triangles[0].vert1.z, 0.001f);
        Assert.AreEqual(24f, obj.CrashBoxes[0][0].x, 0.001f);
        Assert.AreEqual(14f, obj.CrashBoxes[0][0].y, 0.001f);
        Assert.AreEqual(4f, obj.CrashBoxes[0][0].z, 0.001f);
    }

    [TestMethod]
    public void TryGetRenderPosition_LocalWorldPositionStoresCrashOffsetWithScreenSigns()
    {
        var obj = new Engine3dObject
        {
            ObjectId = 3,
            ObjectOffsets = new EngineVector3(5f, 6f, 7f)
        };

        bool positioned = ObjectPlacementMath.TryGetRenderPosition(
            obj,
            localWorldPosition: new EngineVector3(10f, 20f, 30f),
            surfaceAnchor: null,
            screenCenterX: 100,
            screenCenterY: 200,
            static (x, y, z) => new EngineVector3(x, y, z),
            out double x,
            out double y,
            out double z);

        Assert.IsTrue(positioned);
        Assert.AreEqual(95d, x, 0.001d);
        Assert.AreEqual(186d, y, 0.001d);
        Assert.AreEqual(37d, z, 0.001d);
        Assert.IsNotNull(obj.CalculatedCrashOffset);
        Assert.AreEqual(-5f, obj.CalculatedCrashOffset.x, 0.001f);
        Assert.AreEqual(-14f, obj.CalculatedCrashOffset.y, 0.001f);
        Assert.AreEqual(37f, obj.CalculatedCrashOffset.z, 0.001f);
    }

    [TestMethod]
    public void FrameTimingMath_ScalesAgainstBaselineFps()
    {
        Assert.AreEqual(1f, FrameTimingMath.GetFrameScale(1f / 90f), 0.001f);
        Assert.AreEqual(1.5f, FrameTimingMath.GetFrameScale(1f / 60f), 0.001f);
        Assert.AreEqual(FrameTimingMath.DefaultGameplayBaselineDeltaTime, FrameTimingMath.ClampDeltaTime(0f), 0.001f);
    }

    private sealed class EngineTriangleMeshWithColor : EngineTriangleMesh, ITriangleMeshWithColor
    {
        public string? Color { get; set; }
    }
}
