using RetroMesh.Engine;

namespace _3DSpesificsUnitTests.Engine;

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
}
