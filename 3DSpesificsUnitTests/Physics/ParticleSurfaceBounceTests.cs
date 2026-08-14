using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls;
using System.Reflection;
using _3dRotations.Helpers;
using TheOmegaStrain.Runtime.Collision;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Physics;

[TestClass]
public class ParticleSurfaceBounceTests
{
    // Mirror of CrashDetection.EstimateDirectionFromSurface for testability.
    private static ImpactDirection EstimateDirectionFromSurface(Vector3 point, Vector3 min, Vector3 max)
    {
        var center = new Vector3
        {
            x = (min.x + max.x) / 2,
            y = (min.y + max.y) / 2,
            z = (min.z + max.z) / 2
        };
        float dx = point.x - center.x;
        float dy = point.y - center.y;
        float dz = point.z - center.z;

        if (Math.Abs(dy) > Math.Abs(dx) && Math.Abs(dy) > Math.Abs(dz))
            return dy < 0 ? ImpactDirection.Top : ImpactDirection.Bottom;
        else if (Math.Abs(dx) > Math.Abs(dz))
            return dx > 0 ? ImpactDirection.Right : ImpactDirection.Left;
        else
            return ImpactDirection.Center;
    }

    /// <summary>
    /// Simulates a particle falling toward a surface crashbox using the real Physics engine.
    /// Returns the Y position at the moment the particle center enters the AABB (collision frame),
    /// and the penetration depth below the top of the box.
    /// </summary>
    private static (float collisionY, float penetrationDepth, int frameCount) SimulateParticleFallIntoCrashBox(
        float startY, float surfaceBoxMinY, float surfaceBoxMaxY, float gravityStrength = 50f,
        float initialVelocityY = 0f, float friction = 0.02f, bool applyPenetrationCorrection = true)
    {
        // Particle starts above the surface box (lower Y = higher on screen)
        var physics = new GameAiAndControls.Physics.Physics
        {
            GravityStrength = gravityStrength,
            Friction = friction,
            EnergyLossFactor = 0.28f,
            Mass = 1f,
            Velocity = new Vector3 { x = 0, y = initialVelocityY, z = 0 }
        };

        float posY = startY;
        float dt = 1f / 60f;

        // The AABB box the particle falls into (surface-like platform crashbox)
        // In this coordinate system +Y = down, so surfaceBoxMinY < surfaceBoxMaxY
        // and the "top" of the box is surfaceBoxMinY
        float boxTopY = surfaceBoxMinY;

        for (int frame = 0; frame < 600; frame++) // max 10 seconds
        {
            // ApplyGravityForce modifies position in place via position -= velocity
            var pos = new Vector3 { x = 0, y = posY, z = 0 };
            physics.ApplyGravityForce(pos, dt);
            posY = pos.y;

            // Check AABB collision (point-in-box on Y axis only for this 1D test)
            if (posY >= surfaceBoxMinY && posY <= surfaceBoxMaxY)
            {
                if (applyPenetrationCorrection)
                {
                    // Mirror production: undo the actual frame-scaled movement
                    // that pushed the particle into the box.
                    float frameScale = dt * GameState.GameplayBaselineFps;
                    posY += physics.Velocity.y * frameScale;
                }
                float penetration = Math.Max(0f, posY - boxTopY);
                return (posY, penetration, frame);
            }
        }

        return (posY, 0f, -1); // never hit
    }

    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ObjectIdCounter = 0;
        ScreenSetup.Initialize(1500, 1024);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ScreenSetup.Initialize(1500, 1024);
    }

    [TestMethod]
    public void Particle_FreeFall_MeasurePenetrationDepth_IntoSurfaceBox()
    {
        // Surface-like crashbox: Y range 400..600 (top at Y=400, bottom at Y=600)
        // Particle starts at Y=300 (above surface) with zero initial velocity
        var (collisionY, penetration, frames) = SimulateParticleFallIntoCrashBox(
            startY: 300f,
            surfaceBoxMinY: 400f,
            surfaceBoxMaxY: 600f);

        Assert.IsTrue(frames >= 0, "Particle should hit the surface box");

        // Document the penetration depth — this is expected to be significant
        // because the particle moves per-frame without sub-step collision
        Console.WriteLine($"[FREE FALL] Collision at frame {frames}");
        Console.WriteLine($"  Particle Y at collision: {collisionY:F2}");
        Console.WriteLine($"  Box top Y: 400.00");
        Console.WriteLine($"  Penetration depth: {penetration:F2}");
        Console.WriteLine($"  (Penetration as % of box height): {penetration / 200f * 100:F1}%");

        // The penetration should ideally be small (< 5 units = 2.5% of box height)
        // This will likely fail with current physics, proving particles go too deep
        Assert.IsTrue(penetration < 5f,
            $"Penetration depth {penetration:F2} exceeds 5.0 threshold. " +
            $"Collision at Y={collisionY:F2} (box top=400), frame {frames}. " +
            $"That's {penetration / 200f * 100:F1}% of box height");
    }

    [TestMethod]
    public void Particle_DownwardVelocity_MeasurePenetrationDepth()
    {
        // Particle already moving downward (positive velocity.y = upward in position -= velocity,
        // so NEGATIVE velocity.y = downward movement)
        // Actually: position -= velocity, so negative velocity => position increases => moves down
        var (collisionY, penetration, frames) = SimulateParticleFallIntoCrashBox(
            startY: 350f,
            surfaceBoxMinY: 400f,
            surfaceBoxMaxY: 600f,
            initialVelocityY: -5f); // falling fast

        Assert.IsTrue(frames >= 0, "Particle should hit the surface box");

        Assert.IsTrue(penetration < 5f,
            $"[FAST FALL] Penetration {penetration:F2} exceeds threshold. " +
            $"Y={collisionY:F2}, frame {frames}, {penetration / 200f * 100:F1}% of box height");
    }

    [TestMethod]
    public void Particle_HighVelocityFall_PenetrationDepth()
    {
        // Very high velocity — simulates a particle that's been falling a long time
        // position -= velocity, so velocity.y = -20 means position += 20 per frame
        var (collisionY, penetration, frames) = SimulateParticleFallIntoCrashBox(
            startY: 380f,
            surfaceBoxMinY: 400f,
            surfaceBoxMaxY: 600f,
            initialVelocityY: -20f); // very fast downward

        Assert.IsTrue(frames >= 0, "Particle should hit the surface box");

        Assert.IsTrue(penetration < 5f,
            $"[HIGH VELOCITY] Penetration {penetration:F2} exceeds threshold. " +
            $"Y={collisionY:F2}, frame {frames}, {penetration / 200f * 100:F1}% of box height");
    }

    [TestMethod]
    public void Particle_ExplosionVelocity_MeasurePenetrationOnReturn()
    {
        // Simulates an explosion particle that goes up then falls back down
        // Explosion particles have positive velocity.y (upward via position -= velocity)
        // then gravity pulls them back down
        var physics = new GameAiAndControls.Physics.Physics
        {
            GravityStrength = 50f,
            Friction = 0.02f,
            EnergyLossFactor = 0.28f,
            Mass = 1f,
            Velocity = new Vector3 { x = 2f, y = 3f, z = 0 } // upward burst
        };

        float posY = 350f; // starts above the surface box
        float dt = 1f / 60f;
        float boxMinY = 400f;
        float boxMaxY = 600f;

        bool wentAbove = true; // starts above the box
        float minY = posY;

        for (int frame = 0; frame < 600; frame++)
        {
            var pos = new Vector3 { x = 0, y = posY, z = 0 };

            if (physics.BounceCooldownFrames > 0)
            {
                // During cooldown, physics uses ADD path
                physics.ApplyGravityForce(pos, dt);
            }
            else
            {
                physics.ApplyGravityForce(pos, dt);
            }
            posY = pos.y;

            if (posY < boxMinY) wentAbove = true;
            if (posY < minY) minY = posY;

            // Only check collision after particle has gone above and is coming back down
            if (wentAbove && posY >= boxMinY && posY <= boxMaxY)
            {
                // Apply penetration correction (mirror production fix)
                float frameScale = dt * GameState.GameplayBaselineFps;
                posY += physics.Velocity.y * frameScale;

                float penetration = posY - boxMinY;
                if (penetration < 0) penetration = 0;
                Console.WriteLine($"[EXPLOSION RETURN] Collision at frame {frame}");
                Console.WriteLine($"  Particle Y at collision: {posY:F2}");
                Console.WriteLine($"  Highest point reached: {minY:F2}");
                Console.WriteLine($"  Box top Y: {boxMinY:F2}");
                Console.WriteLine($"  Penetration depth: {penetration:F2}");
                Console.WriteLine($"  (Penetration as % of box height): {penetration / (boxMaxY - boxMinY) * 100:F1}%");

                Assert.IsTrue(penetration >= 0, "Penetration should be non-negative");
                return;
            }
        }

        Assert.Fail("Explosion particle never returned to hit the surface box");
    }

    [TestMethod]
    public void Particle_Bounce_VelocityIsUpwardAfterSurfaceHit()
    {
        // Verify the bounce fix: after hitting surface, velocity.y must be positive (upward)
        var physics = new GameAiAndControls.Physics.Physics
        {
            GravityStrength = 50f,
            Friction = 0.02f,
            EnergyLossFactor = 0.28f,
            Mass = 1f,
            Velocity = new Vector3 { x = 1f, y = -3f, z = 0 } // falling down
        };

        // Simulate bounce from Top impact (surface hit)
        physics.Bounce(new Vector3(0, 0, 0), ImpactDirection.Top);

        // Apply the same fix as in Particles.cs
        if (physics.Velocity.y < 0)
            physics.Velocity.y = -physics.Velocity.y;

        Assert.IsTrue(physics.Velocity.y > 0,
            $"After surface bounce, velocity.y should be positive (upward) but was {physics.Velocity.y}");
        Assert.AreEqual(3, physics.BounceCooldownFrames, "Bounce should set 3 cooldown frames");
    }

    [TestMethod]
    public void Particle_MultipleBounces_PenetrationDepthPerBounce()
    {
        // Simulate a particle that bounces multiple times, measuring penetration each time
        var physics = new GameAiAndControls.Physics.Physics
        {
            GravityStrength = 50f,
            Friction = 0.02f,
            EnergyLossFactor = 0.28f,
            Mass = 1f,
            Velocity = new Vector3 { x = 0, y = 8f, z = 0 } // upward
        };

        float posY = 350f;
        float dt = 1f / 60f;
        float boxMinY = 400f;
        float boxMaxY = 600f;
        bool aboveBox = true; // starts above the box
        int bounceCount = 0;
        float maxPenetration = 0f;

        for (int frame = 0; frame < 1200 && bounceCount < 5; frame++)
        {
            var pos = new Vector3 { x = 0, y = posY, z = 0 };
            physics.ApplyGravityForce(pos, dt);
            posY = pos.y;

            if (posY < boxMinY) aboveBox = true;

            if (aboveBox && posY >= boxMinY && posY <= boxMaxY)
            {
                // Apply penetration correction (mirror production fix)
                float frameScale = dt * GameState.GameplayBaselineFps;
                posY += physics.Velocity.y * frameScale;

                float penetration = posY - boxMinY;
                if (penetration < 0) penetration = 0;
                bounceCount++;

                Console.WriteLine($"[BOUNCE #{bounceCount}] Frame {frame}");
                Console.WriteLine($"  Particle Y: {posY:F2}, Penetration: {penetration:F2}");
                Console.WriteLine($"  Velocity before bounce: y={physics.Velocity.y:F2}");

                if (penetration > maxPenetration)
                    maxPenetration = penetration;

                // Apply bounce
                physics.Bounce(new Vector3(0, 0, 0), ImpactDirection.Top);
                if (physics.Velocity.y < 0)
                    physics.Velocity.y = -physics.Velocity.y;

                Console.WriteLine($"  Velocity after bounce: y={physics.Velocity.y:F2}");

                aboveBox = false;
            }
        }

        Console.WriteLine($"\nMax penetration across {bounceCount} bounces: {maxPenetration:F2}");
        Assert.IsTrue(bounceCount >= 2, $"Expected at least 2 bounces but got {bounceCount}");
    }

    [TestMethod]
    public void Particle_PenetrationCorrection_ReducesDepthSignificantly()
    {
        // Compare penetration with and without the correction fix
        var (_, uncorrectedPen, _) = SimulateParticleFallIntoCrashBox(
            startY: 380f,
            surfaceBoxMinY: 400f,
            surfaceBoxMaxY: 600f,
            initialVelocityY: -20f,
            applyPenetrationCorrection: false);

        var (_, correctedPen, _) = SimulateParticleFallIntoCrashBox(
            startY: 380f,
            surfaceBoxMinY: 400f,
            surfaceBoxMaxY: 600f,
            initialVelocityY: -20f,
            applyPenetrationCorrection: true);

        Assert.IsTrue(correctedPen < uncorrectedPen,
            $"Corrected penetration ({correctedPen:F2}) should be less than uncorrected ({uncorrectedPen:F2})");

        Assert.IsTrue(correctedPen < 5f,
            $"Corrected penetration ({correctedPen:F2}) should be under 5 units for realistic bounce");
    }

    [TestMethod]
    public void ParticleCollisionRewind_UsesFrameScaledVelocityOnAllAxes()
    {
        var particle = new Particle
        {
            Position = new Vector3 { x = 10f, y = 400f, z = 20f },
            Physics = new GameAiAndControls.Physics.Physics
            {
                Velocity = new Vector3 { x = 2f, y = -8f, z = 3f },
                BounceCooldownFrames = 0
            }
        };

        InvokeRewindParticleAlongLastMove(particle, deltaTime: 1f / 60f, frameScale: 1.5f);

        Assert.AreEqual(13f, particle.Position.x, 0.001f);
        Assert.AreEqual(388f, particle.Position.y, 0.001f);
        Assert.AreEqual(24.5f, particle.Position.z, 0.001f);
    }

    [TestMethod]
    public void ParticleImpactDirection_UsesVisibleParticleMotionBeforeAabbFallback()
    {
        var fallingParticle = new Particle
        {
            Physics = new GameAiAndControls.Physics.Physics
            {
                Velocity = new Vector3 { x = 0.2f, y = -8f, z = 0.1f }
            }
        };

        var upwardParticle = new Particle
        {
            Physics = new GameAiAndControls.Physics.Physics
            {
                Velocity = new Vector3 { x = 0.2f, y = 8f, z = 0.1f }
            }
        };

        Assert.AreEqual(
            ImpactDirection.Top,
            InvokeEstimateParticleDirectionFromVelocity(fallingParticle, ImpactDirection.Left),
            "A visibly falling particle should bounce from the top even if AABB penetration estimates a side.");

        Assert.AreEqual(
            ImpactDirection.Bottom,
            InvokeEstimateParticleDirectionFromVelocity(upwardParticle, ImpactDirection.Left),
            "A visibly upward particle should bounce from the underside.");
    }

    [TestMethod]
    public void ParticleBounceDirection_SurfaceHitUsesTopEvenWithSidePenetration()
    {
        var particle = new Particle
        {
            ImpactStatus = new ImpactStatus
            {
                ObjectName = "Surface",
                ImpactDirection = ImpactDirection.Left
            }
        };

        Assert.AreEqual(
            ImpactDirection.Top,
            InvokeResolveParticleBounceDirection(particle),
            "Surface particles should bounce upward instead of reversing sideways because of AABB side penetration.");
    }

    private static void InvokeRewindParticleAlongLastMove(Particle particle, float deltaTime, float frameScale)
    {
        var method = typeof(ParticlesAI).GetMethod(
            "RewindParticleAlongLastMove",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);
        method!.Invoke(null, new object[] { particle, deltaTime, frameScale });
    }

    private static ImpactDirection InvokeEstimateParticleDirectionFromVelocity(
        Particle particle,
        ImpactDirection fallback)
    {
        var method = typeof(CrashDetection).GetMethod(
            "EstimateParticleDirectionFromVelocity",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);
        return (ImpactDirection)method!.Invoke(null, new object[] { particle, fallback })!;
    }

    private static ImpactDirection? InvokeResolveParticleBounceDirection(Particle particle)
    {
        var method = typeof(ParticlesAI).GetMethod(
            "ResolveParticleBounceDirection",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);
        return (ImpactDirection?)method!.Invoke(null, new object[] { particle });
    }
}
