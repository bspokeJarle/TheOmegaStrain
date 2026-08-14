using RetroMesh.Engine;

namespace RetroMesh.Engine.Tests;

[TestClass]
public class EnginePhysicsMotionMathTests
{
    private const float BaselineFps = 90f;

    [TestMethod]
    public void CalculateThrustForces_RemainsFrameRateIndependent()
    {
        float distanceAt60 = SimulateHorizontalThrust(60);
        float distanceAt90 = SimulateHorizontalThrust(90);
        float tolerance = MathF.Max(0.01f, MathF.Abs(distanceAt90) * 0.02f);

        Assert.AreEqual(distanceAt90, distanceAt60, tolerance);
    }

    [TestMethod]
    public void ApplyGravityForce_UsesCooldownMotionBeforeGravity()
    {
        var position = new EngineVector3(10f, 20f, 30f);
        var velocity = new EngineVector3(3f, -6f, 9f);

        var step = PhysicsMotionMath.ApplyGravityForce(
            position,
            velocity,
            new EngineVector3(100f, 100f, 100f),
            bounceCooldownFrames: 3,
            gravityStrength: 34f,
            deltaTime: 1f / BaselineFps,
            baselineFps: BaselineFps);

        Assert.AreEqual(10f + 3f / BaselineFps, step.Position.x, 0.001f);
        Assert.AreEqual(20f - 6f / BaselineFps, step.Position.y, 0.001f);
        Assert.AreEqual(30f + 9f / BaselineFps, step.Position.z, 0.001f);
        Assert.AreEqual(3f, step.Velocity.x, 0.001f);
        Assert.AreEqual(-6f, step.Velocity.y, 0.001f);
        Assert.AreEqual(9f, step.Velocity.z, 0.001f);
        Assert.AreEqual(2, step.BounceCooldownFrames);
    }

    [TestMethod]
    public void ApplyShipRotationInput_AppliesKeyboardAndXboxScaling()
    {
        var settings = new ShipRotationInputSettings(
            RotationAcceleration: 1000f,
            XboxRotationAccelerationMultiplier: 1.35f,
            RotationDrag: 0.90f,
            MaxRotationSpeed: 160f,
            XboxMaxRotationSpeedMultiplier: 1.35f);
        var tuning = new PhysicsTuningProfile(
            InertiaRetentionMultiplier: 1f,
            ThrustMultiplier: 1f,
            RotationAccelerationMultiplier: 1f,
            RotationRetentionMultiplier: 1f);

        var keyboardResult = PhysicsMotionMath.ApplyShipRotationInput(
            new ShipRotationInputState(),
            new ShipRotationInputCommand(
                LeftHeld: false,
                RightHeld: true,
                UpHeld: false,
                DownHeld: false,
                XboxYawInput: 0f,
                XboxPitchInput: 0f,
                UseXboxSpeed: false),
            settings,
            tuning,
            deltaTime: 1f / BaselineFps,
            baselineFps: BaselineFps);
        var xboxResult = PhysicsMotionMath.ApplyShipRotationInput(
            new ShipRotationInputState(),
            new ShipRotationInputCommand(
                LeftHeld: false,
                RightHeld: false,
                UpHeld: false,
                DownHeld: false,
                XboxYawInput: 1f,
                XboxPitchInput: 0f,
                UseXboxSpeed: true),
            settings,
            tuning,
            deltaTime: 1f / BaselineFps,
            baselineFps: BaselineFps);

        Assert.AreEqual(10f, keyboardResult.State.YawVelocity, 0.001f);
        Assert.AreEqual(13.5f, xboxResult.State.YawVelocity, 0.001f);
        Assert.IsTrue(xboxResult.State.YawVelocity > keyboardResult.State.YawVelocity);
    }

    [TestMethod]
    public void ApplyShipRotationInput_EmitsWholeRotationStepsAndKeepsRemainder()
    {
        var result = PhysicsMotionMath.ApplyShipRotationInput(
            new ShipRotationInputState(
                YawVelocity: 0f,
                PitchVelocity: 0f,
                YawAccumulator: 1.25f,
                PitchAccumulator: -1.75f),
            new ShipRotationInputCommand(),
            new ShipRotationInputSettings(
                RotationAcceleration: 1000f,
                XboxRotationAccelerationMultiplier: 1.35f,
                RotationDrag: 0.90f,
                MaxRotationSpeed: 160f,
                XboxMaxRotationSpeedMultiplier: 1.35f),
            new PhysicsTuningProfile(
                InertiaRetentionMultiplier: 1f,
                ThrustMultiplier: 1f,
                RotationAccelerationMultiplier: 1f,
                RotationRetentionMultiplier: 1f),
            deltaTime: 1f / BaselineFps,
            baselineFps: BaselineFps);

        Assert.AreEqual(1, result.YawStep);
        Assert.AreEqual(-1, result.PitchStep);
        Assert.AreEqual(0.25f, result.State.YawAccumulator, 0.001f);
        Assert.AreEqual(-0.75f, result.State.PitchAccumulator, 0.001f);
    }

    private static float SimulateHorizontalThrust(int fps)
    {
        float deltaTime = 1f / fps;
        float inertiaX = 0f;
        float inertiaY = 0f;
        float inertiaZ = 0f;
        float thrustEffect = 0f;
        float verticalLiftFactor = 0f;
        float position = 0f;

        for (int frame = 0; frame < fps; frame++)
        {
            var step = PhysicsMotionMath.CalculateThrustForces(
                inertiaX,
                inertiaY,
                inertiaZ,
                thrustEffect,
                verticalLiftFactor,
                thrust: 10f,
                tiltDegrees: 90f,
                rotationDegrees: 90f,
                thrustMultiplier: 1f,
                gravityAcceleration: 3.6f,
                gravityPullMultiplier: 9.0f,
                thrustSpeedMultiplier: 9.6f,
                thrustHeightMultiplier: 7.0f,
                thrustRampRate: 30.0f,
                verticalLiftRate: 3.0f,
                verticalThrustSmoothing: 0.6f,
                inertiaDrag: 0.92f,
                maxInertia: 45.0f,
                inertiaRetentionMultiplier: 1f,
                deltaTime,
                BaselineFps);

            inertiaX = step.InertiaX;
            inertiaY = step.InertiaY;
            inertiaZ = step.InertiaZ;
            thrustEffect = step.ThrustEffect;
            verticalLiftFactor = step.VerticalLiftFactor;

            position += inertiaX * deltaTime * BaselineFps;
        }

        return position;
    }
}
