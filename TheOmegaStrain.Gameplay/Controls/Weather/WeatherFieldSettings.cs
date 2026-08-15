namespace GameAiAndControls.Controls.Weather
{
    public readonly record struct WeatherFieldSettings(
        float DepthStartZ,
        float VisibleDepthSpread,
        float BehindSpread,
        float AheadSpread,
        float OffscreenMargin,
        float DirectionalSpawnAheadMin,
        float DirectionalSpawnAheadMax,
        float DirectionalLateralSpreadFactor,
        float TravelBehindRecycleDistance,
        float TravelAheadRecycleDistance,
        int DirectionalSpawnModulo,
        float VisibleSpreadScreenMultiplier,
        float WorldSpreadScreenMultiplier,
        double OutsideSpawnChance = 0.65d)
    {
        public float VisibleDepthEndZ => DepthStartZ + VisibleDepthSpread;
        public float MaxDepthZ => DepthStartZ + VisibleDepthSpread + AheadSpread;
    }
}
