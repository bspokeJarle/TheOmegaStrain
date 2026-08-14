using RetroMesh.Engine;

namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineCollisionPairMathTests
{
    [TestMethod]
    public void TryCreateBoxCollision_ReturnsCentersDistanceAndDirections()
    {
        var boxA = Box(-10f, -10f, -10f, 10f, 10f, 10f);
        var boxB = Box(5f, -10f, -10f, 25f, 10f, 10f);

        bool collided = CollisionPairMath.TryCreateBoxCollision(
            boxIndexA: 2,
            boxIndexB: 7,
            boxA,
            boxB,
            new CollisionMargins(0f, 0f, 0f),
            out var result);

        Assert.IsTrue(collided);
        Assert.AreEqual(2, result.BoxIndexA);
        Assert.AreEqual(7, result.BoxIndexB);
        Assert.AreEqual(0f, result.CenterA.x, 0.001f);
        Assert.AreEqual(15f, result.CenterB.x, 0.001f);
        Assert.AreEqual(15f, result.CenterDistance, 0.001f);
        Assert.AreEqual(ImpactDirection.Left, result.DirectionA);
        Assert.AreEqual(ImpactDirection.Right, result.DirectionB);
    }

    [TestMethod]
    public void CheckBoxOverlap_ReportsAxisOverlapBeforeCollisionResult()
    {
        var boxA = Box(-10f, -10f, -10f, 10f, 10f, 10f);
        var boxB = Box(12f, -10f, -10f, 30f, 10f, 10f);

        var noMargin = CollisionPairMath.CheckBoxOverlap(
            boxA,
            boxB,
            new CollisionMargins(0f, 0f, 0f));
        var withMargin = CollisionPairMath.CheckBoxOverlap(
            boxA,
            boxB,
            new CollisionMargins(2f, 0f, 0f));

        Assert.IsFalse(noMargin.Overlaps);
        Assert.IsFalse(noMargin.XOverlaps);
        Assert.IsTrue(noMargin.YOverlaps);
        Assert.IsTrue(noMargin.ZOverlaps);

        Assert.IsTrue(withMargin.Overlaps);
        Assert.IsTrue(withMargin.XOverlaps);
    }

    [TestMethod]
    public void TryCreateParticleCollision_UsesVisibleMovementDirection()
    {
        var particleBox = Box(-1f, 395f, -1f, 1f, 397f, 1f);
        var targetBox = Box(-10f, 390f, -10f, 10f, 410f, 10f);

        bool collided = CollisionPairMath.TryCreateParticleCollision(
            particleBox,
            targetBox,
            new EngineVector3(0.1f, -8f, 0.1f),
            out var result);

        Assert.IsTrue(collided);
        Assert.AreEqual(396f, result.ParticleCenter.y, 0.001f);
        Assert.AreEqual(ImpactDirection.Top, result.TargetDirection);
        Assert.AreEqual(ImpactDirection.Top, result.ParticleDirection);
        Assert.AreEqual(390f, result.TargetBounds.MinY, 0.001f);
        Assert.AreEqual(410f, result.TargetBounds.MaxY, 0.001f);
    }

    [TestMethod]
    public void CollisionBoxScanner_ScanBoxCollisions_CanContinueAfterRejectedOverlap()
    {
        var a = ObjectWithBoxes(
            Box(0f, 0f, 0f, 10f, 10f, 10f),
            Box(40f, 0f, 0f, 50f, 10f, 10f));
        var b = ObjectWithBoxes(
            Box(5f, 0f, 0f, 15f, 10f, 10f),
            Box(45f, 0f, 0f, 55f, 10f, 10f));
        var handled = new List<string>();

        bool accepted = CollisionBoxScanner.ScanBoxCollisions(
            a,
            b,
            new CollisionMargins(0f, 0f, 0f),
            CreateWorldBox,
            (in CollisionBoxScanResult<Engine3dObject, EngineVector3> collision) =>
            {
                handled.Add($"{collision.BoxIndexA}:{collision.BoxIndexB}");
                return collision.BoxIndexA == 1;
            });

        Assert.IsTrue(accepted);
        CollectionAssert.AreEqual(new[] { "0:0", "1:1" }, handled);
    }

    [TestMethod]
    public void CollisionBoxScanner_TryFindFirstParticleCollision_ReturnsParticleAndTargetBoxIndices()
    {
        var particle = ObjectWithBoxes(
            Box(100f, 100f, 100f, 102f, 102f, 102f),
            Box(-1f, 395f, -1f, 1f, 397f, 1f));
        var target = ObjectWithBoxes(
            Box(-10f, 390f, -10f, 10f, 410f, 10f));

        bool collided = CollisionBoxScanner.TryFindFirstParticleCollision(
            particle,
            target,
            new EngineVector3(0.1f, -8f, 0.1f),
            CreateWorldBox,
            out ParticleCollisionScanResult<Engine3dObject, EngineVector3> result);

        Assert.IsTrue(collided);
        Assert.AreEqual(1, result.ParticleBoxIndex);
        Assert.AreEqual(0, result.TargetBoxIndex);
        Assert.AreEqual(ImpactDirection.Top, result.Collision.TargetDirection);
        Assert.AreEqual(ImpactDirection.Top, result.Collision.ParticleDirection);
    }

    private static List<IVector3> Box(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        return new List<IVector3>
        {
            new EngineVector3(minX, maxY, minZ),
            new EngineVector3(maxX, maxY, minZ),
            new EngineVector3(maxX, minY, minZ),
            new EngineVector3(minX, minY, minZ),
            new EngineVector3(minX, maxY, maxZ),
            new EngineVector3(maxX, maxY, maxZ),
            new EngineVector3(maxX, minY, maxZ),
            new EngineVector3(minX, minY, maxZ)
        };
    }

    private static Engine3dObject ObjectWithBoxes(params List<IVector3>[] boxes)
    {
        return new Engine3dObject
        {
            ObjectId = 1,
            ObjectName = "TestObject",
            CrashBoxes = new List<List<IVector3>>(boxes)
        };
    }

    private static List<EngineVector3> CreateWorldBox(
        Engine3dObject obj,
        int boxIndex,
        List<IVector3> localBox)
    {
        var offset = obj.ObjectOffsets ?? new EngineVector3();
        var result = new List<EngineVector3>(localBox.Count);

        for (int i = 0; i < localBox.Count; i++)
        {
            var point = localBox[i];
            result.Add(new EngineVector3(
                point.x + offset.x,
                point.y + offset.y,
                point.z + offset.z));
        }

        return result;
    }
}
