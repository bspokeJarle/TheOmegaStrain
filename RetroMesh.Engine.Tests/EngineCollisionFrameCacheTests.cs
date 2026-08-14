using RetroMesh.Engine;

namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineCollisionFrameCacheTests
{
    [TestMethod]
    public void GetWorldBoxPoints_AppliesEffectiveOffsetAndCachesPerObjectReference()
    {
        var cache = new CollisionFrameCache<Engine3dObject, EngineVector3>();
        var obj = CreateObject(objectId: 1, offsetX: 100f, offsetY: 200f, offsetZ: 300f);

        var first = cache.GetWorldBoxPoints(obj, 0, obj.CrashBoxes[0], CreateVector);
        var second = cache.GetWorldBoxPoints(obj, 0, obj.CrashBoxes[0], CreateVector);

        Assert.AreSame(first, second);
        Assert.AreEqual(100f, first[0].x, 0.001f);
        Assert.AreEqual(210f, first[0].y, 0.001f);
        Assert.AreEqual(300f, first[0].z, 0.001f);
        Assert.IsTrue(cache.CacheHits >= 1);
        Assert.IsTrue(cache.CacheMisses >= 1);
    }

    [TestMethod]
    public void GetCenter_UsesWorldCrashPointsAndResetsPerFrame()
    {
        var cache = new CollisionFrameCache<Engine3dObject, EngineVector3>();
        var obj = CreateObject(objectId: 2, offsetX: 50f, offsetY: 60f, offsetZ: 70f);

        var centerBeforeReset = cache.GetCenter(obj, CreateVector);
        cache.ResetFrame();
        obj.ObjectOffsets = new EngineVector3(150f, 160f, 170f);
        var centerAfterReset = cache.GetCenter(obj, CreateVector);

        Assert.AreEqual(55f, centerBeforeReset.x, 0.001f);
        Assert.AreEqual(65f, centerBeforeReset.y, 0.001f);
        Assert.AreEqual(75f, centerBeforeReset.z, 0.001f);
        Assert.AreEqual(155f, centerAfterReset.x, 0.001f);
        Assert.AreEqual(165f, centerAfterReset.y, 0.001f);
        Assert.AreEqual(175f, centerAfterReset.z, 0.001f);
    }

    [TestMethod]
    public void GetOffset_PrefersCalculatedCrashOffset()
    {
        var cache = new CollisionFrameCache<Engine3dObject, EngineVector3>();
        var obj = CreateObject(objectId: 3, offsetX: 1f, offsetY: 2f, offsetZ: 3f);
        obj.CalculatedCrashOffset = new EngineVector3(10f, 20f, 30f);

        var offset = cache.GetOffset(obj, CreateVector);

        Assert.AreEqual(10f, offset.x, 0.001f);
        Assert.AreEqual(20f, offset.y, 0.001f);
        Assert.AreEqual(30f, offset.z, 0.001f);
    }

    private static Engine3dObject CreateObject(int objectId, float offsetX, float offsetY, float offsetZ)
    {
        return new Engine3dObject
        {
            ObjectId = objectId,
            ObjectName = "TestObject",
            ObjectOffsets = new EngineVector3(offsetX, offsetY, offsetZ),
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new EngineVector3(0f, 10f, 0f),
                    new EngineVector3(10f, 10f, 0f),
                    new EngineVector3(10f, 0f, 0f),
                    new EngineVector3(0f, 0f, 0f),
                    new EngineVector3(0f, 10f, 10f),
                    new EngineVector3(10f, 10f, 10f),
                    new EngineVector3(10f, 0f, 10f),
                    new EngineVector3(0f, 0f, 10f)
                }
            }
        };
    }

    private static EngineVector3 CreateVector(float x, float y, float z) => new(x, y, z);
}
