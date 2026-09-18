using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Helpers;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class RocketHelpersTests
{
    private const float MaxRange = 2000f;
    private const float Cooldown = 10f;

    [TestMethod]
    public void OffScreenAttackShipCannotFire()
    {
        Assert.IsFalse(CanFire(isVisible: false));
    }

    [TestMethod]
    public void ShipOutsideMaxRangeBlocksFire()
    {
        Assert.IsFalse(CanFire(distance: MaxRange + 0.01f));
    }

    [TestMethod]
    public void CooldownShorterThanTenSecondsBlocksFire()
    {
        Assert.IsFalse(CanFire(secondsSinceLaunch: 9.999f));
    }

    [TestMethod]
    public void ActiveRocketBlocksFireAfterCooldown()
    {
        Assert.IsFalse(CanFire(secondsSinceLaunch: Cooldown, activeRocketCount: 1));
    }

    [TestMethod]
    public void ExactlyTenSecondsAllowsFire()
    {
        Assert.IsTrue(CanFire(secondsSinceLaunch: Cooldown));
    }

    [TestMethod]
    public void FailedLaunchDoesNotImplicitlyStartCooldown()
    {
        Assert.IsFalse(CanFire(isVisible: false, secondsSinceLaunch: float.PositiveInfinity));
        Assert.IsTrue(CanFire(isVisible: true, secondsSinceLaunch: float.PositiveInfinity));
    }

    [TestMethod]
    public void RocketHasFuelBeforeThreeSeconds()
    {
        Assert.IsTrue(RocketFlightHelpers.HasFuel(2.999f));
    }

    [TestMethod]
    public void RocketHasNoFuelFromThreeSeconds()
    {
        Assert.IsFalse(RocketFlightHelpers.HasFuel(3f));
        Assert.IsFalse(RocketFlightHelpers.HasFuel(4f));
    }

    [TestMethod]
    public void ParticleEmissionStopsWithFuel()
    {
        var powered = Step(elapsed: 2f, delta: 1f / 90f);
        var ballistic = Step(elapsed: 3f, delta: 1f / 90f);

        Assert.IsTrue(powered.ShouldEmitMotorParticles);
        Assert.AreEqual(RocketFlightPhase.Powered, powered.Phase);
        Assert.IsFalse(ballistic.ShouldEmitMotorParticles);
        Assert.AreEqual(RocketFlightPhase.Ballistic, ballistic.Phase);
    }

    [TestMethod]
    public void LaunchDirectionTargetsShipPositionOnceAndCanBeStored()
    {
        var solution = RocketFlightHelpers.CalculateLaunchSolution(
            new Vector3(0f, 0f, 0f),
            new Vector3(30f, 0f, 40f));
        var storedDirection = new Vector3(
            solution.Direction.x,
            solution.Direction.y,
            solution.Direction.z);

        var shipPositionAfterLaunch = new Vector3(-500f, 100f, 700f);

        Assert.AreEqual(50f, solution.DistanceToShip, 0.001f);
        Assert.AreEqual(0.6f, storedDirection.x, 0.001f);
        Assert.AreEqual(0f, storedDirection.y, 0.001f);
        Assert.AreEqual(0.8f, storedDirection.z, 0.001f);
        Assert.AreNotEqual(shipPositionAfterLaunch.x, storedDirection.x);
    }

    [TestMethod]
    public void PoweredMovementIsFrameIndependent()
    {
        var at30Fps = Simulate(elapsed: 0f, frames: 30, delta: 1f / 30f, gravity: 120f);
        var at90Fps = Simulate(elapsed: 0f, frames: 90, delta: 1f / 90f, gravity: 120f);

        AssertVectorEqual(at30Fps.Position, at90Fps.Position);
        Assert.AreEqual(at30Fps.VerticalVelocity, at90Fps.VerticalVelocity, 0.001f);
    }

    [TestMethod]
    public void BallisticMovementIsFrameIndependent()
    {
        var at30Fps = Simulate(elapsed: 5f, frames: 30, delta: 1f / 30f, gravity: 120f);
        var at90Fps = Simulate(elapsed: 5f, frames: 90, delta: 1f / 90f, gravity: 120f);

        AssertVectorEqual(at30Fps.Position, at90Fps.Position);
        Assert.AreEqual(at30Fps.VerticalVelocity, at90Fps.VerticalVelocity, 0.001f);
    }

    [TestMethod]
    public void GravityIncreasesFallVelocityAfterFuelDepletion()
    {
        var step = Step(elapsed: 5f, delta: 0.1f, gravity: 120f, verticalVelocity: 4f);

        Assert.AreEqual(16f, step.VerticalVelocity, 0.001f);
        Assert.IsTrue(step.PositionDelta.y > 0f, "Positive Y moves toward Surface in Omega coordinates.");
    }

    [TestMethod]
    public void HitShipReturnsHitShipLifecycleResult()
    {
        Assert.AreEqual(
            RocketLifecycleResult.HitShip,
            RocketLifecycleHelpers.Classify(true, false, false, 0f, MaxRange, 0f, 30f));
    }

    [TestMethod]
    public void HitSurfaceReturnsHitSurfaceLifecycleResult()
    {
        Assert.AreEqual(
            RocketLifecycleResult.HitSurface,
            RocketLifecycleHelpers.Classify(false, true, false, 0f, MaxRange, 0f, 30f));
    }

    [TestMethod]
    public void SeparateAttackShipInputsDoNotShareCooldownOrActiveRocketState()
    {
        bool firstAttackShip = CanFire(secondsSinceLaunch: 1f, activeRocketCount: 1);
        bool secondAttackShip = CanFire(secondsSinceLaunch: Cooldown, activeRocketCount: 0);

        Assert.IsFalse(firstAttackShip);
        Assert.IsTrue(secondAttackShip);
    }

    private static bool CanFire(
        bool isVisible = true,
        float distance = 500f,
        float secondsSinceLaunch = Cooldown,
        int activeRocketCount = 0)
    {
        return RocketFireHelpers.CanFire(
            isVisible,
            distance,
            MaxRange,
            secondsSinceLaunch,
            Cooldown,
            activeRocketCount);
    }

    private static RocketFlightStep Step(
        float elapsed,
        float delta,
        float gravity = 120f,
        float verticalVelocity = 0f)
    {
        return RocketFlightHelpers.CalculateStep(
            new Vector3(0.6f, 0f, 0.8f),
            forwardSpeed: 900f,
            verticalVelocity,
            elapsed,
            delta,
            gravity);
    }

    private static (Vector3 Position, float VerticalVelocity) Simulate(
        float elapsed,
        int frames,
        float delta,
        float gravity)
    {
        var position = new Vector3();
        float verticalVelocity = 0f;

        for (int i = 0; i < frames; i++)
        {
            var step = Step(elapsed, delta, gravity, verticalVelocity);
            position.x += step.PositionDelta.x;
            position.y += step.PositionDelta.y;
            position.z += step.PositionDelta.z;
            verticalVelocity = step.VerticalVelocity;
            elapsed += step.AppliedDeltaSeconds;
        }

        return (position, verticalVelocity);
    }

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.AreEqual(expected.x, actual.x, 0.01f);
        Assert.AreEqual(expected.y, actual.y, 0.01f);
        Assert.AreEqual(expected.z, actual.z, 0.01f);
    }
}
