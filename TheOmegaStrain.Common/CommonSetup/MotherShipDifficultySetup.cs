namespace CommonUtilities.CommonSetup
{
    public static class MotherShipDifficultySetup
    {
        public const float MinAggression = 0.25f;
        public const float MaxAggression = 3.0f;
        public const float MinDirectionUpdateIntervalSeconds = 0.08f;
        public const float MinChargeWindowSeconds = 0.45f;

        public static float GetAggression(float aggression)
        {
            return Math.Clamp(aggression, MinAggression, MaxAggression);
        }

        public static float ScaleCooldown(float baseSeconds, float aggression)
        {
            return baseSeconds / GetAggression(aggression);
        }

        public static float ScaleTurnSpeed(float baseDegreesPerSecond, float aggression)
        {
            return baseDegreesPerSecond * GetAggression(aggression);
        }

        public static float ScaleTravelSpeed(float baseUnitsPerSecond, float aggression)
        {
            return baseUnitsPerSecond * GetAggression(aggression);
        }

        public static int ScaleHealth(int baseHealth, float aggression)
        {
            float healthMultiplier = Math.Max(1.0f, GetAggression(aggression));
            return (int)Math.Round(baseHealth * healthMultiplier);
        }

        public static float ScaleUpdateInterval(float baseSeconds, float aggression)
        {
            return Math.Max(MinDirectionUpdateIntervalSeconds, baseSeconds / GetAggression(aggression));
        }

        public static float ScaleChargeWindow(float baseSeconds, float aggression)
        {
            return Math.Max(MinChargeWindowSeconds, baseSeconds / GetAggression(aggression));
        }
    }
}
