using Domain;

namespace CommonUtilities.CommonSetup
{
    public readonly record struct BiomePhysicsProfile(
        float InertiaRetentionMultiplier,
        float ThrustMultiplier,
        float TravelSpeedMultiplier,
        float RotationAccelerationMultiplier,
        float RotationRetentionMultiplier);

    public static class BiomePhysicsSetup
    {
        public static BiomePhysicsProfile GetProfile(SceneBiomeTypes biome) => biome switch
        {
            SceneBiomeTypes.Winter => new BiomePhysicsProfile(
                InertiaRetentionMultiplier: 0.992f,
                ThrustMultiplier: 0.99f,
                TravelSpeedMultiplier: 0.99f,
                RotationAccelerationMultiplier: 0.99f,
                RotationRetentionMultiplier: 0.998f),

            SceneBiomeTypes.Desert => new BiomePhysicsProfile(
                InertiaRetentionMultiplier: 1.015f,
                ThrustMultiplier: 1.02f,
                TravelSpeedMultiplier: 1.03f,
                RotationAccelerationMultiplier: 1.02f,
                RotationRetentionMultiplier: 1.005f),

            SceneBiomeTypes.Rainforrest => new BiomePhysicsProfile(
                InertiaRetentionMultiplier: 0.996f,
                ThrustMultiplier: 0.995f,
                TravelSpeedMultiplier: 0.998f,
                RotationAccelerationMultiplier: 1.0f,
                RotationRetentionMultiplier: 0.999f),

            _ => new BiomePhysicsProfile(
                InertiaRetentionMultiplier: 1.0f,
                ThrustMultiplier: 1.0f,
                TravelSpeedMultiplier: 1.0f,
                RotationAccelerationMultiplier: 1.0f,
                RotationRetentionMultiplier: 1.0f)
        };

        public static BiomePhysicsProfile CurrentProfile =>
            GetProfile(CommonUtilities.CommonGlobalState.GameState.GamePlayState.CurrentSceneBiome);
    }
}
