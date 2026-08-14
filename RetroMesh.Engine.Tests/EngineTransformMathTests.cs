
namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineTransformMathTests
{
    [TestMethod]
    public void GroundProjectionMath_InterpolatesYInsideTriangle()
    {
        var triangle = new EngineTriangleMeshWithColor
        {
            vert1 = new EngineVector3(0f, 10f, 0f),
            vert2 = new EngineVector3(10f, 20f, 0f),
            vert3 = new EngineVector3(0f, 30f, 10f)
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

    [TestMethod]
    public void ObjectFrameTransformer_RotatesMeshAndCrashBoxesTogether()
    {
        var obj = new Engine3dObject
        {
            ObjectId = 2,
            ObjectName = "RotatingObject",
            Rotation = new EngineVector3(0f, 0f, 90f),
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart
                {
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            vert1 = new EngineVector3(1f, 0f, 0f),
                            vert2 = new EngineVector3(0f, 1f, 0f),
                            vert3 = new EngineVector3(0f, 0f, 1f)
                        }
                    }
                }
            },
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new EngineVector3(1f, 0f, 0f),
                    new EngineVector3(0f, 1f, 0f)
                }
            }
        };

        new ObjectFrameTransformer().RotateObjectGeometry(obj);

        var triangle = obj.ObjectParts[0].Triangles[0];
        Assert.AreEqual(0f, triangle.vert1.x, 0.001f);
        Assert.AreEqual(1f, triangle.vert1.y, 0.001f);
        Assert.AreEqual(-1f, triangle.vert2.x, 0.001f);
        Assert.AreEqual(0f, triangle.vert2.y, 0.001f);
        Assert.AreEqual(0f, obj.CrashBoxes[0][0].x, 0.001f);
        Assert.AreEqual(1f, obj.CrashBoxes[0][0].y, 0.001f);
        Assert.AreEqual(-1f, obj.CrashBoxes[0][1].x, 0.001f);
        Assert.AreEqual(0f, obj.CrashBoxes[0][1].y, 0.001f);
    }

    [TestMethod]
    public void SurfaceGeometryCache_UpdateIndexesLandBasedTriangles()
    {
        var first = new EngineTriangleMeshWithColor
        {
            landBasedPosition = 42,
            vert1 = new EngineVector3(),
            vert2 = new EngineVector3(1f, 0f, 0f),
            vert3 = new EngineVector3(0f, 1f, 0f)
        };
        var second = new EngineTriangleMeshWithColor
        {
            landBasedPosition = null,
            vert1 = new EngineVector3(),
            vert2 = new EngineVector3(2f, 0f, 0f),
            vert3 = new EngineVector3(0f, 2f, 0f)
        };
        var cache = new TestSurfaceGeometryCache
        {
            LandBasedIds = new HashSet<long?> { 1 },
            RotatedSurfaceTriangleByLandId = new Dictionary<long, ITriangleMeshWithColor>
            {
                [1] = second
            }
        };
        var rotatedTriangles = new List<ITriangleMeshWithColor> { first, second };

        SurfaceGeometryCache.Update(cache, rotatedTriangles);

        Assert.AreSame(rotatedTriangles, cache.RotatedSurfaceTriangles);
        Assert.IsTrue(cache.LandBasedIds.Contains(42));
        Assert.IsTrue(cache.LandBasedIds.Contains(null));
        Assert.AreSame(first, cache.RotatedSurfaceTriangleByLandId[42]);
        Assert.IsFalse(cache.RotatedSurfaceTriangleByLandId.ContainsKey(1));
    }

    [TestMethod]
    public void ObjectScreenStateTracker_ResetClearsAndMarksObjectsById()
    {
        var first = new Engine3dObject { ObjectId = 1, ObjectName = "First", IsOnScreen = true };
        var second = new Engine3dObject { ObjectId = 2, ObjectName = "Second", IsOnScreen = true };
        var tracker = new ObjectScreenStateTracker<Engine3dObject>();

        tracker.Reset(new List<Engine3dObject> { first, second });
        tracker.MarkOnScreen(2);

        Assert.AreEqual(2, tracker.Count);
        Assert.IsFalse(first.IsOnScreen);
        Assert.IsTrue(second.IsOnScreen);
    }

    private sealed class TestSurfaceGeometryCache : ISurfaceGeometryCache
    {
        public List<ITriangleMeshWithColor> RotatedSurfaceTriangles { get; set; } = new();
        public Dictionary<long, ITriangleMeshWithColor> RotatedSurfaceTriangleByLandId { get; set; } = new();
        public HashSet<long?> LandBasedIds { get; set; } = new();
    }

    private sealed class EngineTriangleMeshWithColor : EngineTriangleMesh, ITriangleMeshWithColor
    {
        public string? Color { get; set; }
    }
}
