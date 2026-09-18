using System;
using RetroMesh.Engine;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Gameplay.Helpers;

public enum RocketFlightPhase
{
    Powered,
    Transition,
    Ballistic
}

public readonly record struct RocketFlightStep(
    Vector3 PositionDelta,
    float VerticalVelocity,
    RocketFlightPhase Phase,
    bool HasFuel,
    bool ShouldEmitMotorParticles,
    float AppliedDeltaSeconds);

public readonly record struct RocketLaunchSolution(
    Vector3 Direction,
    float DistanceToShip);

public static class RocketFlightHelpers
{
    public const float DefaultFuelDurationSeconds = WeaponSetup.RocketFuelSeconds;

    public static RocketLaunchSolution CalculateLaunchSolution(
        Vector3 rocketWorldPosition,
        Vector3 shipWorldPosition)
    {
        var launchVector = MovementHelpers.GetDirectionAndDistanceWorld(
            rocketWorldPosition,
            shipWorldPosition);
        return new RocketLaunchSolution(launchVector.Direction, launchVector.Length);
    }

    public static bool HasFuel(
        float elapsedFlightSeconds,
        float fuelDurationSeconds = DefaultFuelDurationSeconds)
    {
        return fuelDurationSeconds > 0f &&
               elapsedFlightSeconds >= 0f &&
               elapsedFlightSeconds < fuelDurationSeconds;
    }

    /// <summary>
    /// Pure workshop calculation for fixed-direction flight. The live weapon uses
    /// Weapons.MoveFallingRocket and IPhysics.ApplyForces instead; do not run both.
    /// VerticalVelocity is additional gravity velocity, excluding launchDirection.Y.
    /// </summary>
    public static RocketFlightStep CalculateStep(
        IVector3 launchDirection,
        float forwardSpeed,
        float verticalVelocity,
        float elapsedFlightSeconds,
        float deltaSeconds,
        float gravityAcceleration,
        float fuelDurationSeconds = DefaultFuelDurationSeconds)
    {
        float dt = FrameTimingMath.ClampDeltaTime(deltaSeconds);
        var direction = VectorMath.Normalize(launchDirection);
        var forwardVelocity = VectorMath.Multiply(direction, MathF.Max(0f, forwardSpeed));

        float remainingFuelSeconds = MathF.Max(0f, fuelDurationSeconds - MathF.Max(0f, elapsedFlightSeconds));
        float poweredSeconds = MathF.Min(dt, remainingFuelSeconds);
        float ballisticSeconds = dt - poweredSeconds;

        var delta = new EngineVector3();
        if (poweredSeconds > 0f)
        {
            // Constant powered velocity is the zero-friction case already modelled by the engine.
            var poweredStep = PhysicsMotionMath.ApplyDragForce(
                new EngineVector3(),
                forwardVelocity,
                friction: 0f,
                poweredSeconds,
                FrameTimingMath.DefaultGameplayBaselineFps);
            delta = poweredStep.Position;
        }

        float updatedVerticalVelocity = verticalVelocity;
        if (ballisticSeconds > 0f)
        {
            var ballisticVelocity = VectorMath.Add(
                forwardVelocity,
                new EngineVector3(0f, verticalVelocity, 0f));

            // ApplyForces owns the engine's gravity convention: positive Y falls toward Surface.
            var gravityStep = PhysicsMotionMath.ApplyForces(
                new EngineVector3(),
                ballisticVelocity,
                new EngineVector3(),
                bounceCooldownFrames: 0,
                gravityStrength: MathF.Max(0f, gravityAcceleration),
                mass: 1f,
                friction: 0f,
                ballisticSeconds,
                FrameTimingMath.DefaultGameplayBaselineFps);

            updatedVerticalVelocity = gravityStep.Velocity.y - forwardVelocity.y;

            // Average velocity gives a stable constant-gravity displacement at different frame rates.
            var averageVelocity = VectorMath.Multiply(
                VectorMath.Add(ballisticVelocity, gravityStep.Velocity),
                0.5f);
            delta = VectorMath.Add(delta, VectorMath.Multiply(averageVelocity, ballisticSeconds));
        }

        bool hasFuelAfterStep = HasFuel(elapsedFlightSeconds + dt, fuelDurationSeconds);
        RocketFlightPhase phase = poweredSeconds > 0f && ballisticSeconds > 0f
            ? RocketFlightPhase.Transition
            : poweredSeconds > 0f
                ? RocketFlightPhase.Powered
                : RocketFlightPhase.Ballistic;

        return new RocketFlightStep(
            new Vector3(delta.x, delta.y, delta.z),
            updatedVerticalVelocity,
            phase,
            hasFuelAfterStep,
            poweredSeconds > 0f,
            dt);
    }
}
