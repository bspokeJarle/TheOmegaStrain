using Domain;
using static Domain._3dSpecificsImplementations;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using GameAiAndControls.Physics;
using GameAiAndControls.Helpers;
using System.Dynamic;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;

public class ParticlesAI : IParticles
{
    private readonly Random random = new();
    private readonly List<IParticle> _deadParticles = new();

    // --- Configuration ---
    private const int MaxParticlesBase = 12;
    private const int MaxThrustMultiplier = 5;
    private const int MaxDynamicParticles = 50;
    private const float MinLife = 2.5f;
    private const float MaxLife = 3.5f;
    private const float MinSize = 1f;
    private const float MaxSize = 4f;
    private const float SpreadIntensity = 3f;
    private const float AccelerationRandomFactor = 0.1f;
    private const float FadeFactor = 0.03f;
    private const float InitialThrottleFactor = 5f;
    private const int MaxParticlesPerEmission = 3;
    private const float DefaultExplosionParticleMultiplier = 2.0f;
    private const float HighExplosionParticleMultiplier = 3.0f;
    private const int SteadyStateRetirePerFrame = 1;
    private const int BurstBuffer = 15;
    private const float MinRetireAgeSeconds = 1.5f;
    private const long DefaultVariedStartMaxTicks = 500_000;

    public float ThrottleDurationFactor { get; set; } = 0.3f;
    public float DynamicCapMultiplier { get; set; } = 1.0f;
    public int BurstMaxParticlesPerEmission { get; set; } = 0;
    public long VariedStartMaxTicks { get; set; } = DefaultVariedStartMaxTicks;
    public bool LastEmissionWasBurst { get; private set; }
    public int LastEmissionParticleCount { get; private set; }
    public List<IParticle> Particles { get; set; } = new();
    public IObjectMovement? ParentShip { get; set; }
    public bool Visible { get; set; }
    public bool EnableParticleLogging { get; set; } = false;
    public float LifeMultiplier { get; set; } = 1.0f;
    public float SizeMultiplier { get; set; } = 1.0f;
    public float GravityStrength { get; set; } = 50f;
    public int MaxParticlesOverride { get; set; } = 0;
    public string? ColorStartOverride { get; set; }
    public string? ColorMidOverride { get; set; }
    public string? ColorEndOverride { get; set; }
    public float? ExplosionParticleMultiplierOverride { get; set; }
    public float ExplosionStartYOffset { get; set; } = -150f;

    private DateTime _lastUpdateTime = DateTime.UtcNow;
    private bool _burstActive = true;

    public void MoveParticles()
    {
        _deadParticles.Clear();
        DateTime now = DateTime.UtcNow;
        long currentTicks = now.Ticks;

        float deltaTime = (float)(now - _lastUpdateTime).TotalSeconds;
        if (deltaTime <= 0f || deltaTime > 1f) deltaTime = GameState.GameplayBaselineDeltaTime;
        deltaTime = Math.Clamp(deltaTime, 0f, 0.1f);
        _lastUpdateTime = now;
        float frameScale = deltaTime * GameState.GameplayBaselineFps;

        var numLogParticles = 0;

        foreach (var particle in Particles)
        {
            if (particle.BirthTime.Ticks + particle.VariedStart > currentTicks) continue;

            long lifeTicks = (long)(particle.Life * 10_000_000);
            long deathTicks = particle.BirthTime.Ticks + lifeTicks + particle.VariedStart;
            long boostTicks = (long)(lifeTicks * ThrottleDurationFactor);

            if (deathTicks > currentTicks)
            {
                particle.Visible = true;
                float lifeProgress = (float)(currentTicks - particle.BirthTime.Ticks - particle.VariedStart) / lifeTicks;

                particle.Size = ApplyFade(particle.Size, lifeProgress);
                particle.ParticleTriangle.Color = GetColorByLifeProgress(lifeProgress, particle);
                if (Logger.ShouldLog(EnableParticleLogging))
                {
                    Logger.Log($"Particle Color: {particle.ParticleTriangle.Color}");
                }

                if (particle.Physics != null)
                {
                    if (particle.ImpactStatus?.HasCrashed == true && particle.ImpactStatus.ImpactDirection != null)
                    {
                        if (Logger.ShouldLog(EnableParticleLogging))
                        {
                            Logger.Log("[Particle Bounce Triggered]");
                            Logger.Log($"   Object Name        : {particle.ImpactStatus.ObjectName}");
                            Logger.Log($"   Position (Local)   : x={particle.Position?.x:F2}, y={particle.Position?.y:F2}, z={particle.Position?.z:F2}");
                            Logger.Log($"   Impact Direction   : {particle.ImpactStatus.ImpactDirection}");
                            Logger.Log($"   Velocity Before    : x={particle.Physics.Velocity.x:F2}, y={particle.Physics.Velocity.y:F2}, z={particle.Physics.Velocity.z:F2}");
                        }

                        RewindParticleAlongLastMove(particle, deltaTime, frameScale);

                        var bounceDirection = ResolveParticleBounceDirection(particle);
                        particle.Physics.Bounce(new Vector3(0, 0, 0), bounceDirection);

                        // Surface hits must always bounce upward (+Y position = down, -Y = up;
                        // but position -= velocity, so positive velocity.y decreases position.y = moves up)
                        if (particle.Physics.Velocity.y < 0)
                            particle.Physics.Velocity.y = -particle.Physics.Velocity.y;

                        particle.Life *= 0.8f;
                        particle.ImpactStatus.HasCrashed = false;
                        particle.ImpactStatus.ImpactDirection = null;

                        if (Logger.ShouldLog(EnableParticleLogging))
                        {
                            Logger.Log("After Bounce Velocity: " + particle.Physics.Velocity);
                        }
                    }

                    long ageTicks = currentTicks - particle.BirthTime.Ticks - particle.VariedStart;
                    if (ageTicks <= boostTicks)
                    {
                        particle.Physics.Velocity = PhysicsHelpers.Add(
                            particle.Physics.Velocity,
                            PhysicsHelpers.Multiply(particle.Physics.Velocity, InitialThrottleFactor * deltaTime)
                        );
                    }

                    particle.Position = particle.Physics.ApplyGravityForce(particle.Position ?? new Vector3(), deltaTime);
                    if (Logger.ShouldLog(EnableParticleLogging))
                    {
                        numLogParticles++;
                        if (numLogParticles > 3)
                        {
                            if (Logger.ShouldLog(EnableParticleLogging))
                            {
                                Logger.Log($"Particle Position: {particle.Position} Visible:{particle.Visible}");
                                Logger.Log($"Particle Velocity: {particle.Physics.Velocity} Visible:{particle.Visible}");
                            }
                        }
                    }
                }
                else
                {
                    particle.Velocity.x += particle.Acceleration.x * frameScale;
                    particle.Velocity.y += particle.Acceleration.y * frameScale;
                    particle.Velocity.z += particle.Acceleration.z * frameScale;

                    float scaledFriction = MathF.Pow(0.95f, frameScale);
                    particle.Velocity.x *= scaledFriction;
                    particle.Velocity.y *= scaledFriction;
                    particle.Velocity.z *= scaledFriction;

                    particle.Position.x -= particle.Velocity.x * frameScale;
                    particle.Position.y -= particle.Velocity.y * frameScale;
                    particle.Position.z -= particle.Velocity.z * frameScale;
                }

                if (particle.Rotation != null && particle.RotationSpeed != null)
                {
                    particle.Rotation.x += particle.RotationSpeed.x * frameScale;
                    particle.Rotation.y += particle.RotationSpeed.y * frameScale;
                    particle.Rotation.z += particle.RotationSpeed.z * frameScale;
                }
            }
            else
            {
                _deadParticles.Add(particle);
            }
        }

        foreach (var deadParticle in _deadParticles)
        {
            Particles.Remove(deadParticle);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ApplyFade(float size, float progress) => size * (1.0f - FadeFactor * progress);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string GetColorByLifeProgress(float progress, IParticle? particle = null)
    {
        // Clamp the progress between 0 and 1
        progress = Clamp(progress, 0f, 1f);

        string? colorStartOverride = ColorStartOverride;
        string? colorMidOverride = ColorMidOverride;
        string? colorEndOverride = ColorEndOverride;
        if (particle is Particle concreteParticle)
        {
            colorStartOverride = concreteParticle.ColorStartOverride ?? colorStartOverride;
            colorMidOverride = concreteParticle.ColorMidOverride ?? colorMidOverride;
            colorEndOverride = concreteParticle.ColorEndOverride ?? colorEndOverride;
        }

        if (colorStartOverride != null && colorMidOverride != null && colorEndOverride != null)
        {
            if (progress < 0.5f)
                return LerpColorHex(colorStartOverride, colorMidOverride, progress / 0.5f);

            return LerpColorHex(colorMidOverride, colorEndOverride, (progress - 0.5f) / 0.5f);
        }

        int r, g, b;

        if (progress < 0.5f)
        {
            // Yellow (255,255,0) to Red (255,0,0)
            float t = progress / 0.5f;
            r = 255;
            g = (int)(255 * (1 - t)); // Fade green from 255 to 0
            b = 0;
        }
        else
        {
            // Red (255,0,0) to Burnt (80,40,0)
            float t = (progress - 0.5f) / 0.5f;
            r = (int)(255 - (175 * t)); // Red from 255 to 80
            g = (int)(0 + (40 * t));    // Green from 0 to 40
            b = 0;
        }

        return $"{ColorMath.ClampColor(r):X2}{ColorMath.ClampColor(g):X2}{ColorMath.ClampColor(b):X2}".ToLower();
    }

    private static string LerpColorHex(string fromHex, string toHex, float amount)
    {
        return ColorMath.LerpColorHex(fromHex, toHex, amount, lowerCase: true, roundChannels: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp(float value, float min, float max) => MathF.Min(MathF.Max(value, min), max);

    private Vector3 GetRandomExplosionDirection()
    {
        double theta = random.NextDouble() * 2 * Math.PI;
        double phi = random.NextDouble() * Math.PI;

        float x = (float)(Math.Sin(phi) * Math.Cos(theta));
        float y = (float)(Math.Cos(phi));
        float z = (float)(Math.Sin(phi) * Math.Sin(theta));

        return new Vector3(x, y, z);
    }

    public void ReleaseParticles(ITriangleMeshWithColor trajectory, ITriangleMeshWithColor startPosition, IVector3 worldPosition, IObjectMovement parentShip, int thrust, bool? explosion, float upwardVelocityBoost = 0f)
    {
        LastEmissionWasBurst = false;
        LastEmissionParticleCount = 0;

        if (thrust == 0)
        {
            Particles.Clear();
            Particles.TrimExcess();
            _burstActive = true;
            return;
        }

        // Prune expired particles before checking the cap. MoveParticles runs
        // after ReleaseParticles, so without this dead particles inflate the
        // count and block all new emissions until they are eventually cleaned up.
        long pruneTicks = DateTime.UtcNow.Ticks;
        Particles.RemoveAll(p =>
        {
            long life = (long)(p.Life * 10_000_000);
            return p.BirthTime.Ticks + life + p.VariedStart <= pruneTicks;
        });

        int dynamicMaxParticles = GetDynamicMaxParticles(thrust);
        if (_burstActive && Particles.Count >= dynamicMaxParticles)
            _burstActive = false;

        // During the initial burst the cap is raised so the jet ignition looks punchy.
        // Once the normal steady-state cap has been reached, the burst ends and the
        // extra particles die off naturally without being replaced.
        int currentCap = _burstActive ? dynamicMaxParticles + BurstBuffer : dynamicMaxParticles;
        int recycledSteadySlots = 0;

        // After the burst, recycle old particles for a smaller continuous stream.
        // If the burst left us above the steady cap, drain one extra particle per frame.
        if (!_burstActive && explosion != true && Particles.Count >= currentCap)
        {
            int steadyEmission = GetSteadyEmissionCount(thrust);
            int overflowDrain = Particles.Count > currentCap ? SteadyStateRetirePerFrame : 0;
            recycledSteadySlots = RetireParticlesForEmission(steadyEmission + overflowDrain);

        }

        if (Particles.Count >= currentCap && recycledSteadySlots <= 0) return;
        if (startPosition == null || trajectory == null) return;

        ParentShip = parentShip;
        int particleCount = Math.Min(thrust * MaxThrustMultiplier, currentCap - Particles.Count);
        //More particles when exploding
        if (explosion == true) particleCount = (int)MathF.Round(particleCount * GetExplosionParticleMultiplier());
        else if (_burstActive && BurstMaxParticlesPerEmission > 0) particleCount = Math.Min(particleCount, BurstMaxParticlesPerEmission);
        else if (!_burstActive)
        {
            int availableSlots = Math.Max(currentCap - Particles.Count, recycledSteadySlots);
            particleCount = Math.Min(GetSteadyEmissionCount(thrust), availableSlots);
        }

        LastEmissionWasBurst = particleCount > 0 && _burstActive && explosion != true;
        LastEmissionParticleCount = particleCount;

        var startPos = new Vector3(
            (startPosition.vert1.x + startPosition.vert2.x + startPosition.vert3.x) / 3,
            (startPosition.vert1.y + startPosition.vert2.y + startPosition.vert3.y) / 3,
            (startPosition.vert1.z + startPosition.vert2.z + startPosition.vert3.z) / 3
        );

        var guidePos = new Vector3(
            (trajectory.vert1.x + trajectory.vert2.x + trajectory.vert3.x) / 3,
            (trajectory.vert1.y + trajectory.vert2.y + trajectory.vert3.y) / 3,
            (trajectory.vert1.z + trajectory.vert2.z + trajectory.vert3.z) / 3
        );

        if (explosion == true) startPos.y += ExplosionStartYOffset;

        if (Logger.ShouldLog(EnableParticleLogging))
        {
            Logger.Log($"[PARTICLES] Releasing {particleCount} particles. Start: ({startPos.x:F2}, {startPos.y:F2}, {startPos.z:F2}), Guide: ({guidePos.x:F2}, {guidePos.y:F2}, {guidePos.z:F2}), World: ({worldPosition.x:F2}, {worldPosition.y:F2}, {worldPosition.z:F2})");
        }

        for (int i = 0; i < particleCount; i++)
        {
            float life = (float)(random.NextDouble() * (MaxLife - MinLife) + MinLife) * LifeMultiplier;
            float size = (float)(random.NextDouble() * (MaxSize - MinSize) + MinSize) * SizeMultiplier;
            float spread = SpreadIntensity * (float)(random.NextDouble() + 0.5);
            //When exploding have a much bigger spread
            if (explosion != null && explosion == true) spread = spread * 2.5f;
            if (explosion != null && explosion == true) size = size * 1.5f;

            float offsetX = (float)(random.NextDouble() - 0.5) * spread;
            float offsetY = (float)(random.NextDouble() - 0.5) * spread;
            float offsetZ = (float)(random.NextDouble() - 0.5) * spread;

            Vector3 velocity;
            if (explosion == true)
            {
                var dir = GetRandomExplosionDirection();

                // Try to bias upward flying particles
                dir.y = MathF.Abs(dir.y) * 0.5f + 0.5f;

                velocity = new Vector3
                {
                    x = dir.x * spread,
                    y = dir.y * spread + Math.Max(0f, upwardVelocityBoost),
                    z = dir.z * spread
                };
            }
            else
            {
                velocity = new Vector3
                {
                    x = (startPos.x - guidePos.x) / life + offsetX,
                    y = (startPos.y - guidePos.y) / life + offsetY,
                    z = (startPos.z - guidePos.z) / life + offsetZ
                };
            }

            velocity.x = Clamp(velocity.x, -10f, 10f);
            velocity.y = Clamp(velocity.y, -10f, 10f);
            velocity.z = Clamp(velocity.z, -10f, 10f);

            var acceleration = new Vector3
            {
                x = (float)(random.NextDouble() - 0.5) * AccelerationRandomFactor,
                y = (float)(random.NextDouble() - 0.5) * AccelerationRandomFactor,
                z = (float)(random.NextDouble() - 0.5) * AccelerationRandomFactor
            };

            var variedStart = VariedStartMaxTicks > 0
                ? random.NextInt64(0, VariedStartMaxTicks)
                : 0;
            //Explosion particles should start at the same time
            if (explosion!=null && explosion == true) variedStart = 0;

            string initialColor = GetColorByLifeProgress(0f);
            Particles.Add(new Particle
            {
                Life = life,
                Size = size,
                Velocity = velocity,
                Acceleration = acceleration,
                VariedStart = variedStart,
                ParticleTriangle = new TriangleMeshWithColor
                {
                    Color = initialColor,
                    vert1 = new Vector3 { x = -size / 2, y = -size / 2, z = 0 },
                    vert2 = new Vector3 { x = size / 2, y = -size / 2, z = 0 },
                    vert3 = new Vector3 { x = 0, y = size / 2, z = 0 },
                    noHidden = true
                },
                Position = new Vector3 { x = startPos.x, y = startPos.y, z = startPos.z },
                WorldPosition = new Vector3 { x = worldPosition.x, y = worldPosition.y, z = worldPosition.z },
                BirthTime = DateTime.UtcNow,
                noHidden = true,
                IsRotated = false,
                Rotation = new Vector3(random.NextInt64(0, 360), random.NextInt64(0, 360), random.NextInt64(0, 360)),
                RotationSpeed = new Vector3(random.NextInt64(-5, 5), random.NextInt64(-5, 5), random.NextInt64(-5, 5)),
                Color = initialColor,
                ColorStartOverride = ColorStartOverride,
                ColorMidOverride = ColorMidOverride,
                ColorEndOverride = ColorEndOverride,
                Visible = false,
                //Physics = null,
                Physics = new Physics
                {
                    Velocity = new Vector3 { x = velocity.x, y = velocity.y, z = velocity.z },
                    Acceleration = new Vector3 { x = acceleration.x, y = acceleration.y, z = acceleration.z },
                    Mass = 1f,
                    GravityStrength = GravityStrength,
                    Friction = 0.02f,
                    EnergyLossFactor = 0.28f,
                },
                ImpactStatus = new ImpactStatus { HasCrashed = false, HasExploded = false }
            });
        }
    }

    private int GetDynamicMaxParticles(int thrust)
    {
        if (MaxParticlesOverride > 0)
            return MaxParticlesOverride;

        float multiplier = Clamp(DynamicCapMultiplier, 0.25f, 4.0f);
        int thrustCap = Math.Max(0, (int)MathF.Round((thrust * 2 + MaxParticlesBase) * multiplier));
        int maxCap = Math.Max(0, (int)MathF.Round(MaxDynamicParticles * multiplier));
        return Math.Min(maxCap, thrustCap);
    }

    private static int GetSteadyEmissionCount(int thrust)
    {
        return Math.Min(thrust * MaxThrustMultiplier, MaxParticlesPerEmission);
    }

    private float GetExplosionParticleMultiplier()
    {
        if (ExplosionParticleMultiplierOverride.HasValue)
            return Clamp(ExplosionParticleMultiplierOverride.Value, 0.25f, 4.0f);

        return GameState.SettingsState?.GraphicsQuality == GraphicsQualityPreset.High
            ? HighExplosionParticleMultiplier
            : DefaultExplosionParticleMultiplier;
    }

    private int RetireParticlesForEmission(int targetCount)
    {
        if (targetCount <= 0)
            return 0;

        int retired = 0;
        long nowRetire = DateTime.UtcNow.Ticks;
        long minAgeTicks = (long)(MinRetireAgeSeconds * TimeSpan.TicksPerSecond);

        while (retired < targetCount && Particles.Count > 0)
        {
            int removeIndex = FindMatureParticleIndex(nowRetire, minAgeTicks);
            if (removeIndex < 0)
                removeIndex = 0;

            Particles.RemoveAt(removeIndex);
            retired++;
        }

        return retired;
    }

    private int FindMatureParticleIndex(long nowTicks, long minAgeTicks)
    {
        for (int i = 0; i < Particles.Count; i++)
        {
            long age = nowTicks - Particles[i].BirthTime.Ticks - Particles[i].VariedStart;
            if (age >= minAgeTicks)
                return i;
        }

        return -1;
    }

    private static void RewindParticleAlongLastMove(IParticle particle, float deltaTime, float frameScale)
    {
        if (particle.Position == null || particle.Physics?.Velocity == null)
            return;

        var velocity = particle.Physics.Velocity;

        if (particle.Physics.BounceCooldownFrames > 0)
        {
            particle.Position.x -= velocity.x * deltaTime;
            particle.Position.y -= velocity.y * deltaTime;
            particle.Position.z -= velocity.z * deltaTime;
            return;
        }

        particle.Position.x += velocity.x * frameScale;
        particle.Position.y += velocity.y * frameScale;
        particle.Position.z += velocity.z * frameScale;
    }

    private static ImpactDirection? ResolveParticleBounceDirection(IParticle particle)
    {
        if (particle.ImpactStatus == null)
            return null;

        if (string.Equals(particle.ImpactStatus.ObjectName, "Surface", StringComparison.Ordinal))
            return ImpactDirection.Top;

        return particle.ImpactStatus.ImpactDirection;
    }
}

public class Particle : IParticle
{
    public DateTime BirthTime { get; set; } = DateTime.Now;
    public ITriangleMeshWithColor ParticleTriangle { get; set; }
    public IVector3 Velocity { get; set; }
    public IVector3 Acceleration { get; set; }
    public float Life { get; set; }
    public float Size { get; set; }
    public string Color { get; set; }
    public string? ColorStartOverride { get; set; }
    public string? ColorMidOverride { get; set; }
    public string? ColorEndOverride { get; set; }
    public bool noHidden { get; set; }
    public long VariedStart { get; set; }
    public bool IsRotated { get; set; }
    public IVector3? Rotation { get; set; }
    public IVector3? Position { get; set; }
    public IVector3 WorldPosition { get; set; }
    public IVector3 GlobalMapPosition { get; set; }
    public IVector3? RotationSpeed { get; set; }
    public bool? NoShading { get; set; }
    public bool Visible { get; set; }
    public IImpactStatus? ImpactStatus { get; set; }
    public IPhysics? Physics { get; set; } = new Physics();
}
