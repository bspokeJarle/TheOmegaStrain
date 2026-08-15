using RetroMesh.Engine;

namespace _3DSpesificsUnitTests.Physics;

[TestClass]
public class CollisionDirectionMathTests
{
    [TestMethod]
    public void EstimateDirection_UsesDominantAxis()
    {
        Assert.AreEqual(
            ImpactDirection.Top,
            CollisionDirectionMath.EstimateDirection(
                new EngineVector3(0f, -10f, 0f),
                new EngineVector3()));

        Assert.AreEqual(
            ImpactDirection.Right,
            CollisionDirectionMath.EstimateDirection(
                new EngineVector3(10f, 2f, 0f),
                new EngineVector3()));

        Assert.AreEqual(
            ImpactDirection.Center,
            CollisionDirectionMath.EstimateDirection(
                new EngineVector3(0f, 2f, 8f),
                new EngineVector3()));
    }

    [TestMethod]
    public void EstimateDirectionFromAabb_UsesPointRelativeToBoxCenter()
    {
        var min = new EngineVector3(-10f, 100f, -10f);
        var max = new EngineVector3(10f, 300f, 10f);

        Assert.AreEqual(
            ImpactDirection.Top,
            CollisionDirectionMath.EstimateDirectionFromAabb(
                new EngineVector3(0f, 90f, 0f),
                min,
                max));

        Assert.AreEqual(
            ImpactDirection.Bottom,
            CollisionDirectionMath.EstimateDirectionFromAabb(
                new EngineVector3(0f, 310f, 0f),
                min,
                max));
    }

    [TestMethod]
    public void EstimateDirectionFromVisibleMovement_AccountsForPositionMinusVelocityPhysics()
    {
        Assert.AreEqual(
            ImpactDirection.Top,
            CollisionDirectionMath.EstimateDirectionFromVisibleMovement(
                new EngineVector3(0.2f, -8f, 0.1f),
                ImpactDirection.Left));

        Assert.AreEqual(
            ImpactDirection.Bottom,
            CollisionDirectionMath.EstimateDirectionFromVisibleMovement(
                new EngineVector3(0.2f, 8f, 0.1f),
                ImpactDirection.Left));

        Assert.AreEqual(
            ImpactDirection.Left,
            CollisionDirectionMath.EstimateDirectionFromVisibleMovement(
                new EngineVector3(),
                ImpactDirection.Left));
    }
}
