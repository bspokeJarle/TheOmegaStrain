using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Helpers;
using RetroMesh.Engine;
using static Domain._3dSpecificsImplementations;
using CommonUtilities.CommonSetup;
using static GameAiAndControls.Helpers.PhysicsHelpers;

namespace GameAiAndControls.Physics
{
    public class Physics : IPhysics
    {
        private bool LocalEnableLogging = false;
        private const float DEG2RAD = MathF.PI / 180f;
        private const string DebrisShimmerColor = "fff2b8";
        private const float BalancedDebrisShimmerStrength = 0.16f;
        private const float HighDebrisShimmerStrength = 0.30f;

        // -- General physics ------------------------------------------
        public float Mass { get; set; } = 1.0f;
        public IVector3 Velocity { get; set; } = new Vector3(0, -90f, 0);
        public float Thrust { get; set; }
        public float Friction { get; set; } = 0.0f;
        public float MaxSpeed { get; set; } = 10.0f;
        public float MaxThrust { get; set; } = 20.0f;
        public IVector3 Acceleration { get; set; } = new Vector3(0, 0, 0);

        // -- Gravity & bounce (used by particle/object physics) -------
        public float GravityStrength { get; set; } = 1f;
        public IVector3 GravitySource { get; set; } = new Vector3 { x = 0, y = -10f, z = 0 };
        public float BounceHeightMultiplier { get; set; } = 0.8f;
        public float EnergyLossFactor { get; set; } = 0.2f;
        public int BounceCooldownFrames { get; set; } = 0;

        // -- Flight inertia state (reset between thrust activations) --
        public float FallVelocity { get; set; } = 0f;
        public float InertiaX { get; set; } = 0f;
        public float InertiaY { get; set; } = 0f;
        public float InertiaZ { get; set; } = 0f;
        public float ThrustEffect { get; set; } = 0f;
        public float VerticalLiftFactor { get; set; } = 0f;

        // -- Flight tuning constants ----------------------------------
        private const float DefaultCeilingHeightScreenFactor = 1.6f;
        private const float DefaultCeilingHeightReduction = 200f;
        private float? _ceilingHeightOverride;

        public float GravityAcceleration { get; set; } = 3.6f;
        public float TerminalFallSpeed { get; set; } = 35f;
        public float GravityPullMultiplier { get; set; } = 9.0f;
        public float ThrustSpeedMultiplier { get; set; } = 9.6f;
        public float ThrustHeightMultiplier { get; set; } = 7.0f;
        public float ThrustRampRate { get; set; } = 30.0f;
        public float InertiaDrag { get; set; } = 0.92f;
        public float MaxInertia { get; set; } = 45.0f;
        public float VerticalThrustSmoothing { get; set; } = 0.6f;
        public float VerticalLiftRate { get; set; } = 3.0f;

        // -- Height limits --------------------------------------------
        public float CeilingHeight
        {
            get => _ceilingHeightOverride ?? PhysicsMotionMath.CalculateCeilingHeight(ScreenSetup.screenSizeY, DefaultCeilingHeightScreenFactor, DefaultCeilingHeightReduction);
            set => _ceilingHeightOverride = value;
        }

        public float FloorHeight { get; set; } = -100f;
        // Computed from current screen size so the value stays correct even
        // when Physics is constructed before ScreenSetup.Initialize().
        public float MaxScreenDrop
        {
            get => PhysicsMotionMath.CalculateMaxScreenDrop(ScreenSetup.screenSizeY, 0.44f);
            set { }
        }

        // -- Hover/float after thrust release -------------------------
        // When thrust stops, gravity stays at HoverMinGravityScale for
        // HoverFloatDuration seconds, then ramps linearly to full over
        // HoverRampDuration seconds.
        public float HoverElapsed { get; set; } = 0f;
        public float HoverFloatDuration { get; set; } = 0.4f;
        public float HoverRampDuration { get; set; } = 0.75f;
        public float HoverMinGravityScale { get; set; } = 0.05f;

        // -- Airborne settle (return-to-rest while not thrusting) -----
        // Gentle spring rate that pulls the surface back toward its
        // resting screen position and zero altitude when airborne.
        // Scaled by 1/ScreenScaleY so the gravity-settle equilibrium
        // distance stays proportional to the screen height, ensuring
        // the ship can always reach the surface crash box.
        // Computed from current screen size so the value stays correct even
        // when Physics is constructed before ScreenSetup.Initialize().
        public float AirborneSettleRate
        {
            get => PhysicsMotionMath.CalculateAirborneSettleRate(ScreenSetup.ScreenScaleY);
            set { }
        }

        // Applies speed-dependent drag (Aviator-inspired v² scaling) and clamps inertia.
        // At low speeds the drag factor is close to InertiaDrag (0.92); at MaxInertia the
        // effective multiplier drops to ~0.85, giving a natural top-speed feel.
        private const float DragSpeedScaling = 0.08f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ApplyDragAndClamp(float inertia, float deltaTime)
        {
            var biomePhysics = BiomePhysicsSetup.CurrentProfile;
            return PhysicsMotionMath.ApplyDragAndClamp(
                inertia,
                MaxInertia,
                InertiaDrag,
                biomePhysics.InertiaRetentionMultiplier,
                deltaTime,
                GameState.GameplayBaselineFps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ConsumeFrameCooldown(int frames, float deltaTime)
        {
            return PhysicsMotionMath.ConsumeFrameCooldown(
                frames,
                deltaTime,
                GameState.GameplayBaselineFps);
        }

        private static Vector3 ToVector(IVector3 vector)
        {
            return new Vector3(vector.x, vector.y, vector.z);
        }

        // Applies drag to the current velocity and returns the updated position
        public IVector3 ApplyDragForce(IVector3 currentPosition, float deltaTime)
        {
            var step = PhysicsMotionMath.ApplyDragForce(
                currentPosition,
                Velocity,
                Friction,
                deltaTime,
                GameState.GameplayBaselineFps);
            Velocity = ToVector(step.Velocity);
            return ToVector(step.Position);
        }

        // Applies gravity, acceleration and drag and returns the updated position
        public IVector3 ApplyForces(IVector3 currentPosition, float deltaTime)
        {
            var step = PhysicsMotionMath.ApplyForces(
                currentPosition,
                Velocity,
                Acceleration,
                BounceCooldownFrames,
                GravityStrength,
                Mass,
                Friction,
                deltaTime,
                GameState.GameplayBaselineFps);
            BounceCooldownFrames = step.BounceCooldownFrames;
            Velocity = ToVector(step.Velocity);
            return ToVector(step.Position);
        }

        // Applies only gravity to the object
        public IVector3 ApplyGravityForce(IVector3 currentPosition, float deltaTime)
        {
            bool mutatesPosition = BounceCooldownFrames <= 0;
            var step = PhysicsMotionMath.ApplyGravityForce(
                currentPosition,
                Velocity,
                Acceleration,
                BounceCooldownFrames,
                GravityStrength,
                deltaTime,
                GameState.GameplayBaselineFps);
            BounceCooldownFrames = step.BounceCooldownFrames;
            Velocity = ToVector(step.Velocity);

            if (!mutatesPosition)
                return ToVector(step.Position);

            currentPosition.x = step.Position.x;
            currentPosition.y = step.Position.y;
            currentPosition.z = step.Position.z;
            return currentPosition;
        }

        // Applies thrust to the object in a specific direction
        public IVector3 ApplyThrust(IVector3 currentPosition, IVector3 direction, float deltaTime)
        {
            var step = PhysicsMotionMath.ApplyThrust(
                currentPosition,
                Velocity,
                direction,
                BounceCooldownFrames,
                Thrust,
                Mass,
                MaxSpeed,
                deltaTime,
                GameState.GameplayBaselineFps);
            BounceCooldownFrames = step.BounceCooldownFrames;
            Velocity = ToVector(step.Velocity);
            return ToVector(step.Position);
        }

        // Reflects velocity along a surface normal and applies energy loss
        public void Bounce(Vector3 normal, ImpactDirection? direction = null)
        {
            Velocity = ToVector(PhysicsMotionMath.BounceVelocity(
                Velocity,
                normal,
                direction,
                EnergyLossFactor));

            BounceCooldownFrames = 3;
        }


        // Applies rotational damping proportional to current rotation rates (Aviator-inspired).
        // Returns a damped copy of the input rotation vector.
        public IVector3 ApplyRotationDragForce(IVector3 rotationVector)
        {
            return ToVector(PhysicsMotionMath.ApplyRotationDragForce(
                rotationVector,
                BiomePhysicsSetup.CurrentProfile.RotationRetentionMultiplier,
                GameState.FrameScale90));
        }

        // Gently returns tilt toward neutral (x?0) when no pitch input is applied.
        // StabilizationRate controls how quickly the tilt decays per call.
        private const float StabilizationRate = 0.03f;

        public void TiltStabilization(ref IVector3 tiltState)
        {
            tiltState.x = PhysicsMotionMath.StabilizeTiltX(tiltState.x, GameState.FrameScale90);
        }

        // Applies gravity when falling (no thrust). Returns updated InertiaY.
        // Gravity is scaled by the hover ramp: near-zero during float, then gradually increasing.
        public float ApplyFallGravity(float rotationDegrees, float deltaTime)
        {
            var step = PhysicsMotionMath.ApplyFallGravity(
                InertiaY,
                HoverElapsed,
                GravityAcceleration,
                GravityPullMultiplier,
                HoverFloatDuration,
                HoverRampDuration,
                HoverMinGravityScale,
                InertiaDrag,
                MaxInertia,
                BiomePhysicsSetup.CurrentProfile.InertiaRetentionMultiplier,
                rotationDegrees,
                deltaTime,
                GameState.GameplayBaselineFps);

            InertiaY = step.InertiaY;
            FallVelocity = step.FallVelocity;
            HoverElapsed = step.HoverElapsed;
            return InertiaY;
        }

        public void ResetHover() => HoverElapsed = 0f;

        // Retained for IPhysics contract — no longer called by ship controls
        public void ReduceFallWithThrust(float thrust, float rotationDegrees, float deltaTime)
        {
            FallVelocity = PhysicsMotionMath.ReduceFallWithThrust(
                FallVelocity,
                thrust,
                rotationDegrees,
                deltaTime);
        }

        // Calculates thrust on all three axes with continuous gravity. Returns updated InertiaY.
        // Tilt controls vertical/forward split; rotation controls heading.
        // When inverted (tilt ~180°), upwardFactor goes negative — thrust pushes into the ground.
        // Gravity scales in with VerticalLiftFactor to prevent an initial dip at thrust start.
        public float CalculateThrustForces(float thrust, float tiltDegrees, float rotationDegrees, float deltaTime)
        {
            var biomePhysics = BiomePhysicsSetup.CurrentProfile;
            var step = PhysicsMotionMath.CalculateThrustForces(
                InertiaX,
                InertiaY,
                InertiaZ,
                ThrustEffect,
                VerticalLiftFactor,
                thrust,
                tiltDegrees,
                rotationDegrees,
                biomePhysics.ThrustMultiplier,
                GravityAcceleration,
                GravityPullMultiplier,
                ThrustSpeedMultiplier,
                ThrustHeightMultiplier,
                ThrustRampRate,
                VerticalLiftRate,
                VerticalThrustSmoothing,
                InertiaDrag,
                MaxInertia,
                biomePhysics.InertiaRetentionMultiplier,
                deltaTime,
                GameState.GameplayBaselineFps);

            InertiaX = step.InertiaX;
            InertiaY = step.InertiaY;
            InertiaZ = step.InertiaZ;
            FallVelocity = step.FallVelocity;
            ThrustEffect = step.ThrustEffect;
            VerticalLiftFactor = step.VerticalLiftFactor;
            return InertiaY;
        }

        public float CalculateCurrentSpeed(bool isLanded)
        {
            return PhysicsMotionMath.CalculateCurrentSpeed(InertiaX, InertiaY, InertiaZ, isLanded);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ClampToHeightRange(float value) => PhysicsMotionMath.ClampToHeightRange(value, FloorHeight, CeilingHeight);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ClampToScreenDrop(float value) => PhysicsMotionMath.ClampToScreenDrop(value, MaxScreenDrop);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float WrapPosition(float position, float diff, float minValue, float maxValue)
        {
            return PhysicsMotionMath.WrapPosition(position, diff, minValue, maxValue);
        }

        private class ExplodingTriangle
        {
            public TriangleMeshWithColor Triangle;
            public Vector3 Direction;
            public Vector3 RotationAxis;
            public float Speed;
            public float RotationSpeed;
            public float ElapsedTime;
            public float Duration = 1.2f; // Adjusted for faster fading
            public Vector3 Center;
            public int PartIndex;
            public int TriangleIndex;
            public string OriginalColor; // NEW: reference color
        }

        // This state crosses LiveGameLoop frame copies. Store copied triangle/vector values here,
        // never references from the current frame object.
        private List<ExplodingTriangle> _explodingTriangles = new();
        private bool _isExploding = false;

        public string? ExplosionColorOverride { get; set; }

        public I3dObject ExplodeObject(I3dObject originalObject, float explosionForce = 200f)
        {
            _explodingTriangles.Clear();
            _isExploding = true;
            RaiseExplosionFlash(explosionForce);

            var explodingObject = OmegaObjectHelpers.DeepCopySingleObject(originalObject);
            var center = CalculateTriangleGeometryCenter(explodingObject);

            int partIndex = 0;

            foreach (var part in explodingObject.ObjectParts)
            {
                int triangleIndex = 0;

                foreach (var triangle in part.Triangles.OfType<TriangleMeshWithColor>())
                {
                    var tri = CopyExplosionTriangle(triangle);

                    var triCenter = GetTriangleCenter(tri);
                    var rawDir = Subtract(triCenter, center);
                    var direction = Normalize(rawDir);

                    // Adjust the explosion direction
                    if (direction.y < 0) direction.y *= 0.25f; // Dampen downward
                    if (direction.y < 0.05f) direction.y += 0.1f; // Lift slightly
                    direction.x *= 1.3f; // More sideways spread
                    direction.z *= 1.3f;
                    direction = Normalize(direction); // Re-normalize

                    var rotationAxis = RandomUnitVector();

                    _explodingTriangles.Add(new ExplodingTriangle
                    {
                        Triangle = tri,
                        Direction = (Vector3)direction,
                        RotationAxis = (Vector3)rotationAxis,
                        Speed = RandomHelper.Float(explosionForce * 0.5f, explosionForce),
                        RotationSpeed = RandomHelper.Float(30f, 120f),
                        Center = (Vector3)triCenter,
                        ElapsedTime = 0f,
                        PartIndex = partIndex,
                        TriangleIndex = triangleIndex,
                        OriginalColor = triangle.Color // Store original color for fading
                    });

                    triangleIndex++;
                }

                partIndex++;
            }

            foreach (var part in originalObject.ObjectParts)
            {
                part.PartName = "ExplodingPart";
            }

            return originalObject;
        }

        private static void RaiseExplosionFlash(float explosionForce)
        {
            if (GameState.SettingsState?.GlowEffectsEnabled != true)
                return;

            float intensity = Math.Clamp(explosionForce / 650f, 0.18f, 0.55f);
            GameState.WeatherVisualState.RaiseImpactFlash(intensity);
        }



        public I3dObject UpdateExplosion(I3dObject explodingObject, DateTime deltaTime)
        {
            if (!_isExploding || _explodingTriangles.Count == 0)
                return explodingObject;

            MarkAsExplodingParts(explodingObject);

            float simulatedElapsedTime = _explodingTriangles[0].ElapsedTime;
            float realElapsedTime = (float)Math.Max(0d, (DateTime.Now - deltaTime).TotalSeconds);
            float frameTime = realElapsedTime - simulatedElapsedTime;

            if (frameTime < 0f)
            {
                frameTime = 0f;
            }

            frameTime = MathF.Min(frameTime, 0.1f);
            bool allTrianglesFinished = true;

            foreach (var exploding in _explodingTriangles)
            { 
                // Progress is capped between 0 and 1
                float progress = Clamp(exploding.ElapsedTime / exploding.Duration, 0f, 1f);

                // Apply color transition based on progress
                string explosionColor = GetExplosionColor(progress, exploding.OriginalColor);
                exploding.Triangle.Color = ApplyDebrisShimmer(explosionColor, exploding, progress);

                if (Logger.ShouldLog(LocalEnableLogging))
                {
                    Logger.Log($"[EXPLOSION] TriangleIndex={exploding.TriangleIndex} " +
                               $"Elapsed={exploding.ElapsedTime:F2}, " +
                               $"RealElapsed={realElapsedTime:F2}, " +
                               $"FrameTime={frameTime:F4}, " +
                               $"Duration={exploding.Duration:F2}, " +
                               $"Progress={progress:F2}, " +
                               $"Color={exploding.Triangle.Color}");
                }

                // Skip movement if done, set Explosion to finished, reset Scene
                if (exploding.ElapsedTime >= exploding.Duration)
                {
                    continue;
                }

                allTrianglesFinished = false;

                // Move and rotate
                exploding.ElapsedTime += frameTime;

                var move = Multiply(exploding.Direction, exploding.Speed * frameTime);
                exploding.Triangle.vert1 = Add(exploding.Triangle.vert1, move);
                exploding.Triangle.vert2 = Add(exploding.Triangle.vert2, move);
                exploding.Triangle.vert3 = Add(exploding.Triangle.vert3, move);

                float angle = exploding.RotationSpeed * frameTime;
                exploding.Triangle.vert1 = RotateAroundAxis(exploding.Triangle.vert1, exploding.RotationAxis, angle, exploding.Center);
                exploding.Triangle.vert2 = RotateAroundAxis(exploding.Triangle.vert2, exploding.RotationAxis, angle, exploding.Center);
                exploding.Triangle.vert3 = RotateAroundAxis(exploding.Triangle.vert3, exploding.RotationAxis, angle, exploding.Center);

                // The render loop rotates frame geometry in-place after movement updates.
                // Keep the physics-owned triangle isolated from that per-frame mutation.
                explodingObject.ObjectParts[exploding.PartIndex].Triangles[exploding.TriangleIndex] =
                    CopyExplosionTriangle(exploding.Triangle);
            }

            if (allTrianglesFinished)
            {
                _isExploding = false;

                if (explodingObject.ImpactStatus != null)
                {
                    explodingObject.ImpactStatus.HasExploded = true;
                }
                else
                {
                    explodingObject.ImpactStatus = new ImpactStatus { HasExploded = true };
                }
            }

            return explodingObject;
        }

        private static void MarkAsExplodingParts(I3dObject explodingObject)
        {
            foreach (var part in explodingObject.ObjectParts)
            {
                part.PartName = "ExplodingPart";
            }
        }

        private static TriangleMeshWithColor CopyExplosionTriangle(TriangleMeshWithColor triangle)
        {
            return new TriangleMeshWithColor
            {
                vert1 = CopyVector(triangle.vert1),
                vert2 = CopyVector(triangle.vert2),
                vert3 = CopyVector(triangle.vert3),
                normal1 = CopyVector(triangle.normal1),
                normal2 = CopyVector(triangle.normal2),
                normal3 = CopyVector(triangle.normal3),
                Color = triangle.Color,
                angle = triangle.angle,
                landBasedPosition = triangle.landBasedPosition,
                noHidden = true
            };
        }

        private static Vector3 CopyVector(IVector3 vector)
        {
            return new Vector3
            {
                x = vector.x,
                y = vector.y,
                z = vector.z
            };
        }

        private string GetExplosionColor(float progress, string originalHex)
        {
            if (ExplosionColorOverride != null)
            {
                if (progress < 0.10f)
                    return LerpColorHex("AADDFF", ExplosionColorOverride, progress / 0.10f);
                else if (progress < 0.35f)
                    return LerpColorHex(ExplosionColorOverride, "1133AA", (progress - 0.10f) / 0.25f);
                else if (progress < 0.7f)
                    return LerpColorHex("1133AA", "001133", (progress - 0.35f) / 0.35f);
                else
                    return LerpColorHex("001133", "000000", (progress - 0.7f) / 0.3f);
            }

            if (progress < 0.10f)
                return LerpColorHex(originalHex, "ffff00", progress / 0.10f); // original ? yellow
            else if (progress < 0.35f)
                return LerpColorHex("ffff00", "ff0000", (progress - 0.10f) / 0.25f); // yellow ? red
            else if (progress < 0.7f)
                return LerpColorHex("ff0000", "330000", (progress - 0.35f) / 0.35f); // red ? dark red
            else
                return LerpColorHex("330000", "000000", (progress - 0.7f) / 0.3f); // dark red ? black
        }

        private static string ApplyDebrisShimmer(string baseColor, ExplodingTriangle exploding, float progress)
        {
            float maxStrength = GetDebrisShimmerStrength();
            if (maxStrength <= 0f)
                return baseColor;

            float phase = exploding.ElapsedTime * 34f
                          + exploding.PartIndex * 1.73f
                          + exploding.TriangleIndex * 2.41f
                          + exploding.Speed * 0.027f
                          + exploding.RotationSpeed * 0.013f;

            float pulse = 0.5f + 0.5f * MathF.Sin(phase);
            float glint = MathF.Pow(pulse, 3.5f);
            float fade = MathF.Max(0f, 1f - progress);
            float amount = maxStrength * (0.18f + 0.82f * glint) * fade;

            return LerpColorHex(baseColor, DebrisShimmerColor, amount);
        }

        private static float GetDebrisShimmerStrength()
        {
            var settings = GameState.SettingsState;
            return (settings?.GraphicsQuality ?? GraphicsQualityPreset.Balanced) switch
            {
                GraphicsQualityPreset.Low => 0f,
                GraphicsQualityPreset.High => settings?.GlowEffectsEnabled == true
                    ? MathF.Min(0.36f, HighDebrisShimmerStrength + 0.06f)
                    : HighDebrisShimmerStrength,
                _ => BalancedDebrisShimmerStrength
            };
        }
    }
}
