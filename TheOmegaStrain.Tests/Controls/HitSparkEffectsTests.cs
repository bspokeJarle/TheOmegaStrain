using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Helpers;
using System.Collections.Generic;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class HitSparkEffectsTests
{
    [TestMethod]
    public void SparkParticleBudget_FollowsGraphicsQuality()
    {
        Assert.IsTrue(HitSparkEffects.IsWeaponHit("Laser"));

        GameState.SettingsState = new GameSettingsState { GraphicsQuality = GraphicsQualityPreset.Low };
        Assert.AreEqual(0, HitSparkEffects.GetSparkParticleCountForCurrentQuality());

        GameState.SettingsState = new GameSettingsState();
        Assert.AreEqual(10, HitSparkEffects.GetSparkParticleCountForCurrentQuality());

        GameState.SettingsState = new GameSettingsState { GraphicsQuality = GraphicsQualityPreset.High };
        Assert.AreEqual(20, HitSparkEffects.GetSparkParticleCountForCurrentQuality());
    }

    [TestMethod]
    public void ReleaseHitSparks_DefaultCreatesHalfOfHighParticles()
    {
        GameState.SettingsState = new GameSettingsState();
        var defaultTarget = CreateTarget();

        HitSparkEffects.ReleaseHitSparks(defaultTarget, new NoopMovement(), "Bullet");

        Assert.AreEqual(10, defaultTarget.Particles!.Particles.Count);
        Assert.IsTrue(defaultTarget.Particles.Particles.All(p => p.Visible));

        GameState.SettingsState = new GameSettingsState { GraphicsQuality = GraphicsQualityPreset.High };
        var highTarget = CreateTarget();

        HitSparkEffects.ReleaseHitSparks(highTarget, new NoopMovement(), "Bullet");

        Assert.AreEqual(20, highTarget.Particles!.Particles.Count);
        Assert.IsTrue(highTarget.Particles.Particles.All(p => p.Visible));
    }

    [TestMethod]
    public void ReleaseHitSparks_LowGraphicsAndNonWeaponHitsDoNotEmit()
    {
        GameState.SettingsState = new GameSettingsState { GraphicsQuality = GraphicsQualityPreset.Low };
        var lowTarget = CreateTarget();

        HitSparkEffects.ReleaseHitSparks(lowTarget, new NoopMovement(), "Bullet");

        Assert.AreEqual(0, lowTarget.Particles!.Particles.Count);

        GameState.SettingsState = new GameSettingsState();
        var nonWeaponTarget = CreateTarget();

        HitSparkEffects.ReleaseHitSparks(nonWeaponTarget, new NoopMovement(), "Ship");

        Assert.AreEqual(0, nonWeaponTarget.Particles!.Particles.Count);
    }

    [TestMethod]
    public void ReleaseHitSparks_RestoresExistingParticleStyle()
    {
        GameState.SettingsState = new GameSettingsState();
        var particles = new ParticlesAI
        {
            ColorStartOverride = "123456",
            ColorMidOverride = "234567",
            ColorEndOverride = "345678",
            LifeMultiplier = 3.0f,
            SizeMultiplier = 2.0f,
            GravityStrength = 75f,
            ExplosionParticleMultiplierOverride = 1.5f,
            ExplosionStartYOffset = -80f,
            MaxParticlesOverride = 60
        };
        var target = CreateTarget(particles);

        HitSparkEffects.ReleaseHitSparks(target, new NoopMovement(), "Lazer");

        Assert.AreEqual("123456", particles.ColorStartOverride);
        Assert.AreEqual("234567", particles.ColorMidOverride);
        Assert.AreEqual("345678", particles.ColorEndOverride);
        Assert.AreEqual(3.0f, particles.LifeMultiplier);
        Assert.AreEqual(2.0f, particles.SizeMultiplier);
        Assert.AreEqual(75f, particles.GravityStrength);
        Assert.AreEqual(1.5f, particles.ExplosionParticleMultiplierOverride!.Value);
        Assert.AreEqual(-80f, particles.ExplosionStartYOffset);
        Assert.AreEqual(60, particles.MaxParticlesOverride);
        Assert.AreEqual("e8fbff", particles.Particles[0].Color);
    }

    [TestMethod]
    public void ReleaseHitSparks_RaisesTemporaryCapForBusyParticleSystems()
    {
        GameState.SettingsState = new GameSettingsState { GraphicsQuality = GraphicsQualityPreset.High };
        var particles = new ParticlesAI { MaxParticlesOverride = 60 };
        for (int i = 0; i < 60; i++)
            particles.Particles.Add(CreateExistingParticle());
        var target = CreateTarget(particles);

        HitSparkEffects.ReleaseHitSparks(target, new NoopMovement(), "Bullet");

        Assert.AreEqual(80, particles.Particles.Count);
        Assert.AreEqual(60, particles.MaxParticlesOverride);
    }

    private static _3dObject CreateTarget(ParticlesAI? particles = null)
    {
        return new _3dObject
        {
            ObjectId = 1,
            ObjectName = "Seeder",
            WorldPosition = new Vector3 { x = 100f, y = 0f, z = 200f },
            ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 0f },
            Particles = particles ?? new ParticlesAI(),
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "Hull",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "00ff00",
                            noHidden = true,
                            vert1 = new Vector3 { x = -10f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 10f, y = 0f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = -20f, z = 10f }
                        }
                    }
                }
            }
        };
    }

    private static Particle CreateExistingParticle()
    {
        return new Particle
        {
            Life = 3f,
            Size = 1f,
            Velocity = new Vector3(),
            Acceleration = new Vector3(),
            VariedStart = 0,
            ParticleTriangle = new TriangleMeshWithColor
            {
                Color = "ffffff",
                noHidden = true,
                vert1 = new Vector3 { x = -1f, y = -1f, z = 0f },
                vert2 = new Vector3 { x = 1f, y = -1f, z = 0f },
                vert3 = new Vector3 { x = 0f, y = 1f, z = 0f }
            },
            Position = new Vector3(),
            WorldPosition = new Vector3(),
            BirthTime = System.DateTime.UtcNow,
            Color = "ffffff",
            Visible = false,
            Physics = null
        };
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
