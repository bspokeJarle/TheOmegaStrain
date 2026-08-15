using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Controls;
using System.Linq;
using System.Reflection;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class ParticlesAiVisualStyleTests
{
    [TestMethod]
    public void ReleaseParticles_AppliesConfiguredFlameStyleToNewParticles()
    {
        var particles = new ParticlesAI
        {
            ColorStartOverride = "fff8c8",
            ColorMidOverride = "ff8a20",
            ColorEndOverride = "5a1800",
            SizeMultiplier = 2.0f,
            GravityStrength = 34f,
            LifeMultiplier = 0.72f,
            MaxParticlesOverride = 20
        };

        particles.ReleaseParticles(
            CreatePointTriangle(0f, -10f, 0f),
            CreatePointTriangle(0f, 0f, 0f),
            new Vector3(),
            new NoopMovement(),
            thrust: 1,
            explosion: false);

        Assert.IsTrue(particles.Particles.Count > 0);
        var particle = particles.Particles[0];
        Assert.AreEqual("fff8c8", particle.Color);
        Assert.AreEqual("fff8c8", particle.ParticleTriangle.Color);
        Assert.IsTrue(particle.Life < 3.5f, "LifeMultiplier should shorten flame particles so the plume feels energetic.");
        Assert.IsTrue(particle.Size >= 2.0f, "SizeMultiplier should make flame particles visually stronger than the default minimum.");
        Assert.AreEqual(34f, particle.Physics!.GravityStrength);
    }

    [TestMethod]
    public void HighGraphics_UsesRaisedThrustParticleCap()
    {
        GameState.SettingsState = new GameSettingsState
        {
            GraphicsQuality = GraphicsQualityPreset.High,
            ParticleDensityPercent = 180
        };

        var method = typeof(ShipControls).GetMethod(
            "GetThrustParticleCap",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);
        int cap = (int)method!.Invoke(null, null)!;

        Assert.AreEqual(220, cap);
    }

    [TestMethod]
    public void DefaultGraphics_DoesNotApplyEnhancedThrustParticleStyle()
    {
        GameState.SettingsState = new GameSettingsState();
        var particles = new ParticlesAI
        {
            MaxParticlesOverride = 99,
            LifeMultiplier = 0.5f,
            SizeMultiplier = 2.0f,
            ThrottleDurationFactor = 0.1f,
            GravityStrength = 12f,
            ColorStartOverride = "ffffff",
            ColorMidOverride = "ff0000",
            ColorEndOverride = "000000"
        };
        var controls = new ShipControls
        {
            ParentObject = new _3dObject
            {
                ObjectId = 1,
                Particles = particles
            }
        };

        InvokeConfigureThrustParticleStyle(controls);

        Assert.AreEqual(0, particles.MaxParticlesOverride);
        Assert.AreEqual(1.2f, particles.DynamicCapMultiplier);
        Assert.AreEqual(0, particles.BurstMaxParticlesPerEmission);
        Assert.AreEqual(500_000, particles.VariedStartMaxTicks);
        Assert.AreEqual(1.0f, particles.LifeMultiplier);
        Assert.AreEqual(1.0f, particles.SizeMultiplier);
        Assert.AreEqual(0.3f, particles.ThrottleDurationFactor);
        Assert.AreEqual(50f, particles.GravityStrength);
        Assert.IsNull(particles.ColorStartOverride);
        Assert.IsNull(particles.ColorMidOverride);
        Assert.IsNull(particles.ColorEndOverride);
    }

    [TestMethod]
    public void HighGraphics_AppliesEnhancedThrustParticleStyle()
    {
        GameState.SettingsState = new GameSettingsState
        {
            GraphicsQuality = GraphicsQualityPreset.High,
            ParticleDensityPercent = 180
        };
        var particles = new ParticlesAI();
        var controls = new ShipControls
        {
            ParentObject = new _3dObject
            {
                ObjectId = 2,
                Particles = particles
            }
        };

        InvokeConfigureThrustParticleStyle(controls);

        Assert.AreEqual(220, particles.MaxParticlesOverride);
        Assert.AreEqual(1.0f, particles.DynamicCapMultiplier);
        Assert.AreEqual(14, particles.BurstMaxParticlesPerEmission);
        Assert.AreEqual(1_250_000, particles.VariedStartMaxTicks);
        Assert.AreEqual(0.72f, particles.LifeMultiplier);
        Assert.AreEqual(1.55f, particles.SizeMultiplier);
        Assert.AreEqual(0.2f, particles.ThrottleDurationFactor);
        Assert.AreEqual(34f, particles.GravityStrength);
        Assert.AreEqual("fff8c8", particles.ColorStartOverride);
        Assert.AreEqual("ff8a20", particles.ColorMidOverride);
        Assert.AreEqual("5a1800", particles.ColorEndOverride);
    }

    [TestMethod]
    public void DynamicCapMultiplier_RaisesDefaultParticleCapacity()
    {
        int defaultCap = InvokeGetDynamicMaxParticles(new ParticlesAI(), thrust: 10);
        int boostedCap = InvokeGetDynamicMaxParticles(new ParticlesAI
        {
            DynamicCapMultiplier = 1.2f
        }, thrust: 10);

        Assert.AreEqual(32, defaultCap);
        Assert.AreEqual(38, boostedCap);
    }

    [TestMethod]
    public void HighGraphics_IncreasesExplosionParticleBurst()
    {
        var guide = CreatePointTriangle(0f, -10f, 0f);
        var start = CreatePointTriangle(0f, 0f, 0f);

        GameState.SettingsState = new GameSettingsState { GraphicsQuality = GraphicsQualityPreset.Balanced };
        var balancedParticles = new ParticlesAI();
        balancedParticles.ReleaseParticles(guide, start, new Vector3(), new NoopMovement(), thrust: 6, explosion: true);

        GameState.SettingsState = new GameSettingsState { GraphicsQuality = GraphicsQualityPreset.High };
        var highParticles = new ParticlesAI();
        highParticles.ReleaseParticles(guide, start, new Vector3(), new NoopMovement(), thrust: 6, explosion: true);

        Assert.AreEqual(60, balancedParticles.Particles.Count);
        Assert.AreEqual(90, highParticles.Particles.Count);
    }

    [TestMethod]
    public void BurstMaxParticlesPerEmission_SpreadsInitialHighGraphicsBurst()
    {
        var particles = new ParticlesAI
        {
            MaxParticlesOverride = 220,
            BurstMaxParticlesPerEmission = 14,
            VariedStartMaxTicks = 1_250_000
        };

        particles.ReleaseParticles(
            CreatePointTriangle(0f, -10f, 0f),
            CreatePointTriangle(0f, 0f, 0f),
            new Vector3(),
            new NoopMovement(),
            thrust: 10,
            explosion: false);

        Assert.AreEqual(14, particles.Particles.Count);
        Assert.IsTrue(
            particles.Particles.All(p => p.VariedStart >= 0 && p.VariedStart < 1_250_000),
            "High graphics burst particles should be spread within the configured start jitter window.");
    }

    [TestMethod]
    public void SteadyStateAfterBurst_EmitsWhileDrainingBurstOverflow()
    {
        var particles = new ParticlesAI
        {
            MaxParticlesOverride = 12,
            BurstMaxParticlesPerEmission = 5,
            VariedStartMaxTicks = 0
        };
        var guide = CreatePointTriangle(0f, -10f, 0f);
        var start = CreatePointTriangle(0f, 0f, 0f);
        var movement = new NoopMovement();

        particles.ReleaseParticles(guide, start, new Vector3(), movement, thrust: 10, explosion: false);
        particles.ReleaseParticles(guide, start, new Vector3(), movement, thrust: 10, explosion: false);
        particles.ReleaseParticles(guide, start, new Vector3(), movement, thrust: 10, explosion: false);

        Assert.AreEqual(15, particles.Particles.Count);
        Assert.IsTrue(particles.LastEmissionWasBurst);

        particles.ReleaseParticles(guide, start, new Vector3(), movement, thrust: 10, explosion: false);

        Assert.IsFalse(particles.LastEmissionWasBurst);
        Assert.AreEqual(3, particles.LastEmissionParticleCount);
        Assert.AreEqual(14, particles.Particles.Count);
    }

    [TestMethod]
    public void ThrustBurstBoost_IsSmallAndDecays()
    {
        var controls = new ShipControls();

        InvokeStartThrustBurstBoost(controls);

        float initial = InvokeConsumeThrustBurstTravelBoost(controls, deltaTime: 0.14f);
        float faded = InvokeConsumeThrustBurstTravelBoost(controls, deltaTime: 0.14f);
        float expired = InvokeConsumeThrustBurstTravelBoost(controls, deltaTime: 0.01f);

        Assert.IsTrue(initial > 1.0f && initial <= 1.061f);
        Assert.IsTrue(faded > 1.0f && faded < initial);
        Assert.AreEqual(1.0f, expired, 0.001f);
    }

    private static TriangleMeshWithColor CreatePointTriangle(float x, float y, float z)
    {
        return new TriangleMeshWithColor
        {
            noHidden = true,
            vert1 = new Vector3 { x = x, y = y, z = z },
            vert2 = new Vector3 { x = x, y = y, z = z },
            vert3 = new Vector3 { x = x, y = y, z = z }
        };
    }

    private static void InvokeConfigureThrustParticleStyle(ShipControls controls)
    {
        var method = typeof(ShipControls).GetMethod(
            "ConfigureThrustParticleStyle",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method);
        method!.Invoke(controls, null);
    }

    private static int InvokeGetDynamicMaxParticles(ParticlesAI particles, int thrust)
    {
        var method = typeof(ParticlesAI).GetMethod(
            "GetDynamicMaxParticles",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method);
        return (int)method!.Invoke(particles, new object[] { thrust })!;
    }

    private static void InvokeStartThrustBurstBoost(ShipControls controls)
    {
        var method = typeof(ShipControls).GetMethod(
            "StartThrustBurstBoostIfNeeded",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method);
        method!.Invoke(controls, null);
    }

    private static float InvokeConsumeThrustBurstTravelBoost(ShipControls controls, float deltaTime)
    {
        var method = typeof(ShipControls).GetMethod(
            "ConsumeThrustBurstTravelBoost",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method);
        return (float)method!.Invoke(controls, new object[] { deltaTime })!;
    }

    private sealed class NoopMovement : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new GameAiAndControls.Physics.Physics();
        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) => theObject;
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void Dispose() { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
    }
}
