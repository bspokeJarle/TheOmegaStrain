using System;
using static Domain._3dSpecificsImplementations;

namespace Domain
{
    public interface IPhysics
    {
        float Mass { get; set; }
        IVector3 Velocity { get; set; }
        float Thrust { get; set; }
        float Friction { get; set; }
        float MaxSpeed { get; set; }
        float MaxThrust { get; set; }
        float GravityStrength { get; set; }
        IVector3 GravitySource { get; set; }
        IVector3 Acceleration { get; set; }
        int BounceCooldownFrames { get; set; }
        float BounceHeightMultiplier { get; set; }
        IVector3 ApplyDragForce(IVector3 currentPosition, float deltaTime);
        IVector3 ApplyForces(IVector3 currentPosition, float deltaTime);
        IVector3 ApplyGravityForce(IVector3 currentPosition, float deltaTime);
        IVector3 ApplyThrust(IVector3 currentPosition, IVector3 direction, float deltaTime);
        IVector3 ApplyRotationDragForce(IVector3 rotationVector);
        void Bounce(Vector3 normal, ImpactDirection? direction);
        void TiltStabilization(ref IVector3 tiltState);
        string? ExplosionColorOverride { get; set; }
        I3dObject ExplodeObject(I3dObject explodingObject, float explosionForece);
        I3dObject UpdateExplosion(I3dObject explodingObject, DateTime deltaTime);

        float FallVelocity { get; set; }
        float InertiaX { get; set; }
        float InertiaY { get; set; }
        float InertiaZ { get; set; }
        float ThrustEffect { get; set; }
        float VerticalLiftFactor { get; set; }

        float GravityAcceleration { get; set; }
        float TerminalFallSpeed { get; set; }
        float GravityPullMultiplier { get; set; }
        float ThrustSpeedMultiplier { get; set; }
        float ThrustHeightMultiplier { get; set; }
        float ThrustRampRate { get; set; }
        float InertiaDrag { get; set; }
        float MaxInertia { get; set; }
        float VerticalThrustSmoothing { get; set; }
        float VerticalLiftRate { get; set; }
        float CeilingHeight { get; set; }
        float FloorHeight { get; set; }
        float MaxScreenDrop { get; set; }

        float HoverElapsed { get; set; }
        float HoverFloatDuration { get; set; }
        float HoverRampDuration { get; set; }
        float HoverMinGravityScale { get; set; }
        float AirborneSettleRate { get; set; }
        void ResetHover();

        float ApplyFallGravity(float rotationDegrees, float deltaTime);
        void ReduceFallWithThrust(float thrust, float rotationDegrees, float deltaTime);
        float CalculateThrustForces(float thrust, float tiltDegrees, float rotationDegrees, float deltaTime);
        float CalculateCurrentSpeed(bool isLanded);
        float ClampToHeightRange(float value);
        float ClampToScreenDrop(float value);
        float WrapPosition(float position, float diff, float minValue, float maxValue);
    }
}
