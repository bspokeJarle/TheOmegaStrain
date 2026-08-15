using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.Physics;

[TestClass]
public class BiomePhysicsSetupTests
{
    private GamePlayState? _previousGameplayState;

    [TestInitialize]
    public void Setup()
    {
        _previousGameplayState = GameState.GamePlayState;
        GameState.GamePlayState = new GamePlayState
        {
            CurrentSceneBiome = SceneBiomeTypes.HillsWoods
        };
        GameState.DeltaTime = GameState.GameplayBaselineDeltaTime;
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.GamePlayState = _previousGameplayState ?? new GamePlayState();
        GameState.DeltaTime = GameState.GameplayBaselineDeltaTime;
    }

    [TestMethod]
    public void HillsWoodsProfile_IsNeutralBaseline()
    {
        var profile = BiomePhysicsSetup.GetProfile(SceneBiomeTypes.HillsWoods);

        Assert.AreEqual(1.0f, profile.InertiaRetentionMultiplier);
        Assert.AreEqual(1.0f, profile.ThrustMultiplier);
        Assert.AreEqual(1.0f, profile.TravelSpeedMultiplier);
        Assert.AreEqual(1.0f, profile.RotationAccelerationMultiplier);
        Assert.AreEqual(1.0f, profile.RotationRetentionMultiplier);
    }

    [TestMethod]
    public void BiomeProfiles_StaySubtle()
    {
        var biomes = new[]
        {
            SceneBiomeTypes.Winter,
            SceneBiomeTypes.Rainforrest,
            SceneBiomeTypes.Desert
        };

        foreach (var biome in biomes)
        {
            var profile = BiomePhysicsSetup.GetProfile(biome);

            Assert.IsTrue(profile.InertiaRetentionMultiplier is >= 0.95f and <= 1.05f);
            Assert.IsTrue(profile.ThrustMultiplier is >= 0.95f and <= 1.05f);
            Assert.IsTrue(profile.TravelSpeedMultiplier is >= 0.95f and <= 1.05f);
            Assert.IsTrue(profile.RotationAccelerationMultiplier is >= 0.95f and <= 1.05f);
            Assert.IsTrue(profile.RotationRetentionMultiplier is >= 0.95f and <= 1.05f);
        }
    }

    [TestMethod]
    public void WinterAndDesert_ChangeForwardThrustInExpectedDirections()
    {
        float hills = SimulateForwardInertia(SceneBiomeTypes.HillsWoods);
        float winter = SimulateForwardInertia(SceneBiomeTypes.Winter);
        float desert = SimulateForwardInertia(SceneBiomeTypes.Desert);

        Assert.IsTrue(winter < hills, $"Winter should feel slightly heavier than baseline ({winter:F3} >= {hills:F3}).");
        Assert.IsTrue(desert > hills, $"Desert should feel slightly faster than baseline ({desert:F3} <= {hills:F3}).");
    }

    [TestMethod]
    public void CurrentProfile_FollowsGameplayBiome()
    {
        GameState.GamePlayState.CurrentSceneBiome = SceneBiomeTypes.Desert;

        Assert.AreEqual(
            BiomePhysicsSetup.GetProfile(SceneBiomeTypes.Desert),
            BiomePhysicsSetup.CurrentProfile);
    }

    private static float SimulateForwardInertia(SceneBiomeTypes biome)
    {
        GameState.GamePlayState.CurrentSceneBiome = biome;
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();

        for (int frame = 0; frame < 90; frame++)
        {
            physics.CalculateThrustForces(
                thrust: 10f,
                tiltDegrees: 90f,
                rotationDegrees: 0f,
                deltaTime: GameState.GameplayBaselineDeltaTime);
        }

        return MathF.Abs(physics.InertiaZ);
    }
}
