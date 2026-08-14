using System;

namespace RetroMesh.Engine
{
    public readonly record struct PhysicsTuningProfile(
        float InertiaRetentionMultiplier,
        float ThrustMultiplier,
        float RotationAccelerationMultiplier,
        float RotationRetentionMultiplier);

    public readonly record struct PhysicsMotionStep(
        EngineVector3 Position,
        EngineVector3 Velocity,
        int BounceCooldownFrames);

    public readonly record struct FallGravityStep(
        float InertiaY,
        float FallVelocity,
        float HoverElapsed);

    public readonly record struct ThrustForcesStep(
        float InertiaX,
        float InertiaY,
        float InertiaZ,
        float FallVelocity,
        float ThrustEffect,
        float VerticalLiftFactor);

    public readonly record struct ShipRotationInputState(
        float YawVelocity,
        float PitchVelocity,
        float YawAccumulator,
        float PitchAccumulator);

    public readonly record struct ShipRotationInputCommand(
        bool LeftHeld,
        bool RightHeld,
        bool UpHeld,
        bool DownHeld,
        float XboxYawInput,
        float XboxPitchInput,
        bool UseXboxSpeed);

    public readonly record struct ShipRotationInputSettings(
        float RotationAcceleration,
        float XboxRotationAccelerationMultiplier,
        float RotationDrag,
        float MaxRotationSpeed,
        float XboxMaxRotationSpeedMultiplier);

    public readonly record struct ShipRotationInputResult(
        ShipRotationInputState State,
        int YawStep,
        int PitchStep);

    public static class PhysicsMotionMath
    {
        private const float DegToRad = MathF.PI / 180f;
        private const float DragSpeedScaling = 0.08f;
        private const float DefaultRotationalDamping = 0.94f;
        private const float DefaultTiltStabilizationRate = 0.03f;

        public static float ApplyDragAndClamp(
            float inertia,
            float maxInertia,
            float inertiaDrag,
            float inertiaRetentionMultiplier,
            float deltaTime,
            float baselineFps)
        {
            float speedRatio = MathF.Abs(inertia) / maxInertia;
            float drag = inertiaDrag * inertiaRetentionMultiplier
                         - DragSpeedScaling * speedRatio * speedRatio;
            drag = Math.Clamp(drag, 0.01f, 0.99f);
            float scaledDrag = MathF.Pow(drag, deltaTime * baselineFps);
            return Math.Clamp(inertia * scaledDrag, -maxInertia, maxInertia);
        }

        public static int ConsumeFrameCooldown(int frames, float deltaTime, float baselineFps)
        {
            float frameScale = deltaTime * baselineFps;
            return Math.Max(0, frames - Math.Max(1, (int)MathF.Round(frameScale)));
        }

        public static PhysicsMotionStep ApplyDragForce(
            IVector3 currentPosition,
            IVector3 velocity,
            float friction,
            float deltaTime,
            float baselineFps)
        {
            float scaledDrag = MathF.Pow(1f - friction, deltaTime * baselineFps);
            var newVelocity = Multiply(velocity, scaledDrag);
            var newPosition = Add(currentPosition, Multiply(newVelocity, deltaTime));
            return new PhysicsMotionStep(newPosition, newVelocity, 0);
        }

        public static PhysicsMotionStep ApplyForces(
            IVector3 currentPosition,
            IVector3 velocity,
            IVector3 acceleration,
            int bounceCooldownFrames,
            float gravityStrength,
            float mass,
            float friction,
            float deltaTime,
            float baselineFps)
        {
            if (bounceCooldownFrames > 0)
            {
                int remainingCooldown = ConsumeFrameCooldown(bounceCooldownFrames, deltaTime, baselineFps);
                return new PhysicsMotionStep(
                    Add(currentPosition, Multiply(velocity, deltaTime)),
                    Copy(velocity),
                    remainingCooldown);
            }

            var gravityForce = new EngineVector3(0f, gravityStrength / mass, 0f);
            var newVelocity = Add(velocity, Multiply(gravityForce, deltaTime));
            newVelocity = Add(newVelocity, Multiply(acceleration, deltaTime));
            newVelocity = Multiply(newVelocity, MathF.Pow(1f - friction, deltaTime * baselineFps));

            return new PhysicsMotionStep(
                Add(currentPosition, Multiply(newVelocity, deltaTime)),
                newVelocity,
                0);
        }

        public static PhysicsMotionStep ApplyGravityForce(
            IVector3 currentPosition,
            IVector3 velocity,
            IVector3 acceleration,
            int bounceCooldownFrames,
            float gravityStrength,
            float deltaTime,
            float baselineFps)
        {
            if (bounceCooldownFrames > 0)
            {
                int remainingCooldown = ConsumeFrameCooldown(bounceCooldownFrames, deltaTime, baselineFps);
                return new PhysicsMotionStep(
                    Add(currentPosition, Multiply(velocity, deltaTime)),
                    Copy(velocity),
                    remainingCooldown);
            }

            float frameScale = deltaTime * baselineFps;
            var newVelocity = new EngineVector3(
                velocity.x + acceleration.x * frameScale,
                velocity.y + acceleration.y * frameScale - gravityStrength * deltaTime,
                velocity.z + acceleration.z * frameScale);

            float scaledFriction = MathF.Pow(0.95f, frameScale);
            newVelocity = Multiply(newVelocity, scaledFriction);

            return new PhysicsMotionStep(
                new EngineVector3(
                    currentPosition.x - newVelocity.x * frameScale,
                    currentPosition.y - newVelocity.y * frameScale,
                    currentPosition.z - newVelocity.z * frameScale),
                newVelocity,
                0);
        }

        public static PhysicsMotionStep ApplyThrust(
            IVector3 currentPosition,
            IVector3 velocity,
            IVector3 direction,
            int bounceCooldownFrames,
            float thrust,
            float mass,
            float maxSpeed,
            float deltaTime,
            float baselineFps)
        {
            if (bounceCooldownFrames > 0)
            {
                int remainingCooldown = ConsumeFrameCooldown(bounceCooldownFrames, deltaTime, baselineFps);
                return new PhysicsMotionStep(
                    Add(currentPosition, Multiply(velocity, deltaTime)),
                    Copy(velocity),
                    remainingCooldown);
            }

            if (thrust <= 0f)
                return new PhysicsMotionStep(Copy(currentPosition), Copy(velocity), 0);

            var thrustDir = Normalize(direction);
            var thrustForce = Multiply(thrustDir, thrust / mass);
            var newVelocity = Add(velocity, Multiply(thrustForce, deltaTime));

            float speed = Length(newVelocity);
            if (speed > maxSpeed)
                newVelocity = Multiply(Normalize(newVelocity), maxSpeed);

            return new PhysicsMotionStep(
                Add(currentPosition, Multiply(newVelocity, deltaTime)),
                newVelocity,
                0);
        }

        public static EngineVector3 BounceVelocity(
            IVector3 velocity,
            IVector3 normal,
            ImpactDirection? direction,
            float energyLossFactor)
        {
            var resolvedNormal = ResolveBounceNormal(normal, direction);
            var result = Copy(velocity);

            if (resolvedNormal.y != 0f)
                result.y = -result.y * energyLossFactor;

            if (resolvedNormal.x != 0f)
                result.x = -result.x * energyLossFactor;

            if (resolvedNormal.z != 0f)
                result.z = -result.z * energyLossFactor;

            return result;
        }

        public static EngineVector3 ApplyRotationDragForce(
            IVector3 rotationVector,
            float rotationRetentionMultiplier,
            float frameScale,
            float rotationalDamping = DefaultRotationalDamping)
        {
            float biomeDamping = Math.Clamp(
                rotationalDamping * rotationRetentionMultiplier,
                0.01f,
                0.999f);
            float scaledDamping = MathF.Pow(biomeDamping, frameScale);

            return new EngineVector3(
                rotationVector.x * scaledDamping,
                rotationVector.y * scaledDamping,
                rotationVector.z * scaledDamping);
        }

        public static float StabilizeTiltX(
            float tiltX,
            float frameScale,
            float stabilizationRate = DefaultTiltStabilizationRate)
        {
            float scaledRate = 1f - MathF.Pow(1f - stabilizationRate, frameScale);
            return tiltX - tiltX * scaledRate;
        }

        public static FallGravityStep ApplyFallGravity(
            float inertiaY,
            float hoverElapsed,
            float gravityAcceleration,
            float gravityPullMultiplier,
            float hoverFloatDuration,
            float hoverRampDuration,
            float hoverMinGravityScale,
            float inertiaDrag,
            float maxInertia,
            float inertiaRetentionMultiplier,
            float rotationDegrees,
            float deltaTime,
            float baselineFps)
        {
            hoverElapsed += deltaTime;
            float gravityScale = GetHoverGravityScale(
                hoverElapsed,
                hoverFloatDuration,
                hoverRampDuration,
                hoverMinGravityScale);

            float rotationRad = (rotationDegrees % 180f) * DegToRad;
            float gravityModifier = Math.Clamp(MathF.Sin(rotationRad), 0.3f, 1.0f);
            float gravityPull = gravityAcceleration * gravityModifier * gravityPullMultiplier * gravityScale * deltaTime;
            float newInertiaY = ApplyDragAndClamp(
                inertiaY - gravityPull,
                maxInertia,
                inertiaDrag,
                inertiaRetentionMultiplier,
                deltaTime,
                baselineFps);

            return new FallGravityStep(newInertiaY, MathF.Max(-newInertiaY, 0f), hoverElapsed);
        }

        public static float GetHoverGravityScale(
            float hoverElapsed,
            float hoverFloatDuration,
            float hoverRampDuration,
            float hoverMinGravityScale)
        {
            if (hoverElapsed < hoverFloatDuration)
                return hoverMinGravityScale;

            float rampElapsed = hoverElapsed - hoverFloatDuration;
            if (rampElapsed >= hoverRampDuration)
                return 1f;

            return hoverMinGravityScale + (1f - hoverMinGravityScale) * (rampElapsed / hoverRampDuration);
        }

        public static float ReduceFallWithThrust(
            float fallVelocity,
            float thrust,
            float rotationDegrees,
            float deltaTime)
        {
            float upwardFactor = MathF.Cos(rotationDegrees * DegToRad);
            float thrustLift = thrust * upwardFactor * 0.75f * deltaTime;
            return Math.Max(fallVelocity - thrustLift, 0f);
        }

        public static ThrustForcesStep CalculateThrustForces(
            float inertiaX,
            float inertiaY,
            float inertiaZ,
            float thrustEffect,
            float verticalLiftFactor,
            float thrust,
            float tiltDegrees,
            float rotationDegrees,
            float thrustMultiplier,
            float gravityAcceleration,
            float gravityPullMultiplier,
            float thrustSpeedMultiplier,
            float thrustHeightMultiplier,
            float thrustRampRate,
            float verticalLiftRate,
            float verticalThrustSmoothing,
            float inertiaDrag,
            float maxInertia,
            float inertiaRetentionMultiplier,
            float deltaTime,
            float baselineFps)
        {
            float effectiveThrust = thrust * thrustMultiplier;

            thrustEffect = MathF.Min(thrustEffect + thrustRampRate * deltaTime, 1f);
            verticalLiftFactor = MathF.Min(verticalLiftFactor + verticalLiftRate * deltaTime, 1f);

            float tiltRad = tiltDegrees * DegToRad;
            float rotationRad = rotationDegrees * DegToRad;

            float upwardFactor = MathF.Cos(tiltRad);
            float forwardFactor = MathF.Sin(tiltRad);
            float dirX = MathF.Sin(rotationRad);
            float dirZ = MathF.Cos(rotationRad);

            float horizontalForce = effectiveThrust * thrustEffect * thrustSpeedMultiplier * forwardFactor * deltaTime;
            inertiaX = ApplyDragAndClamp(
                inertiaX + horizontalForce * dirX,
                maxInertia,
                inertiaDrag,
                inertiaRetentionMultiplier,
                deltaTime,
                baselineFps);
            inertiaZ = ApplyDragAndClamp(
                inertiaZ - horizontalForce * dirZ,
                maxInertia,
                inertiaDrag,
                inertiaRetentionMultiplier,
                deltaTime,
                baselineFps);

            float verticalThrust = effectiveThrust * thrustEffect * verticalLiftFactor * thrustHeightMultiplier
                                   * upwardFactor * verticalThrustSmoothing * deltaTime;
            float gravityPull = gravityAcceleration * gravityPullMultiplier * verticalLiftFactor * deltaTime;
            inertiaY = ApplyDragAndClamp(
                inertiaY + verticalThrust - gravityPull,
                maxInertia,
                inertiaDrag,
                inertiaRetentionMultiplier,
                deltaTime,
                baselineFps);

            return new ThrustForcesStep(
                inertiaX,
                inertiaY,
                inertiaZ,
                MathF.Max(-inertiaY, 0f),
                thrustEffect,
                verticalLiftFactor);
        }

        public static float CalculateCurrentSpeed(float inertiaX, float inertiaY, float inertiaZ, bool isLanded)
        {
            float horizontalSpeed = MathF.Sqrt(inertiaX * inertiaX + inertiaZ * inertiaZ);
            float verticalSpeed = isLanded ? 0f : MathF.Abs(inertiaY);
            return horizontalSpeed + verticalSpeed;
        }

        public static float CalculateCeilingHeight(
            float screenHeight,
            float screenFactor,
            float reduction)
        {
            return screenHeight * screenFactor - reduction;
        }

        public static float CalculateMaxScreenDrop(float screenHeight, float factor)
        {
            return screenHeight * factor;
        }

        public static float CalculateAirborneSettleRate(float screenScaleY)
        {
            return 2.0f / screenScaleY;
        }

        public static float ClampToHeightRange(float value, float floorHeight, float ceilingHeight)
        {
            return Math.Clamp(value, floorHeight, ceilingHeight);
        }

        public static float ClampToScreenDrop(float value, float maxScreenDrop)
        {
            return MathF.Min(value, maxScreenDrop);
        }

        public static float WrapPosition(float position, float diff, float minValue, float maxValue)
        {
            float newPos = position + diff;
            if (newPos >= maxValue) return minValue;
            if (newPos <= minValue) return maxValue;
            return newPos;
        }

        public static ShipRotationInputResult ApplyShipRotationInput(
            ShipRotationInputState state,
            ShipRotationInputCommand command,
            ShipRotationInputSettings settings,
            PhysicsTuningProfile tuning,
            float deltaTime,
            float baselineFps)
        {
            float yawVelocity = state.YawVelocity;
            float pitchVelocity = state.PitchVelocity;

            float rotationAcceleration = settings.RotationAcceleration * tuning.RotationAccelerationMultiplier;
            if (command.LeftHeld) yawVelocity -= rotationAcceleration * deltaTime;
            if (command.RightHeld) yawVelocity += rotationAcceleration * deltaTime;
            if (command.UpHeld) pitchVelocity += rotationAcceleration * deltaTime;
            if (command.DownHeld) pitchVelocity -= rotationAcceleration * deltaTime;

            float xboxRotationAcceleration = rotationAcceleration * settings.XboxRotationAccelerationMultiplier;
            if (command.XboxYawInput != 0f)
                yawVelocity += command.XboxYawInput * xboxRotationAcceleration * deltaTime;
            if (command.XboxPitchInput != 0f)
                pitchVelocity += command.XboxPitchInput * xboxRotationAcceleration * deltaTime;

            float rotationDragBase = Math.Clamp(
                settings.RotationDrag * tuning.RotationRetentionMultiplier,
                0.01f,
                0.999f);
            float rotationDrag = MathF.Pow(rotationDragBase, deltaTime * baselineFps);
            float maxRotationSpeed = command.UseXboxSpeed
                ? settings.MaxRotationSpeed * settings.XboxMaxRotationSpeedMultiplier
                : settings.MaxRotationSpeed;

            yawVelocity = Math.Clamp(yawVelocity, -maxRotationSpeed, maxRotationSpeed) * rotationDrag;
            pitchVelocity = Math.Clamp(pitchVelocity, -maxRotationSpeed, maxRotationSpeed) * rotationDrag;

            float yawAccumulator = state.YawAccumulator + yawVelocity * deltaTime;
            float pitchAccumulator = state.PitchAccumulator + pitchVelocity * deltaTime;

            int yawStep = (int)yawAccumulator;
            int pitchStep = (int)pitchAccumulator;
            if (yawStep != 0) yawAccumulator -= yawStep;
            if (pitchStep != 0) pitchAccumulator -= pitchStep;

            return new ShipRotationInputResult(
                new ShipRotationInputState(
                    yawVelocity,
                    pitchVelocity,
                    yawAccumulator,
                    pitchAccumulator),
                yawStep,
                pitchStep);
        }

        private static EngineVector3 ResolveBounceNormal(IVector3 normal, ImpactDirection? direction)
        {
            if (!direction.HasValue)
                return Copy(normal);

            return direction.Value switch
            {
                ImpactDirection.Top => new EngineVector3(0f, -1f, 0f),
                ImpactDirection.Bottom => new EngineVector3(0f, 1f, 0f),
                ImpactDirection.Left => new EngineVector3(-1f, 0f, 0f),
                ImpactDirection.Right => new EngineVector3(1f, 0f, 0f),
                ImpactDirection.Center => new EngineVector3(0f, -1f, 0f),
                _ => Copy(normal)
            };
        }

        private static EngineVector3 Copy(IVector3 vector)
        {
            return new EngineVector3(vector.x, vector.y, vector.z);
        }

        private static EngineVector3 Add(IVector3 a, IVector3 b)
        {
            return new EngineVector3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        private static EngineVector3 Multiply(IVector3 vector, float scalar)
        {
            return new EngineVector3(vector.x * scalar, vector.y * scalar, vector.z * scalar);
        }

        private static EngineVector3 Normalize(IVector3 vector)
        {
            float length = Length(vector);
            return length == 0f
                ? new EngineVector3()
                : new EngineVector3(vector.x / length, vector.y / length, vector.z / length);
        }

        private static float Length(IVector3 vector)
        {
            return MathF.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
        }
    }
}
