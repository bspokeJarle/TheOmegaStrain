namespace SteamIntegration;

public static class SteamGameConfig
{
    public const uint DevelopmentAppId = 480;
    public const uint ProductionAppId = 0;

    public static class Leaderboards
    {
        public const string GlobalHighScore = "GLOBAL_HIGHSCORE";
        public const string PlanetClearScore = "PLANET_CLEAR_SCORE";
        public const string LowAltitudeScore = "LOW_ALTITUDE_SCORE";
    }

    public static class Achievements
    {
        public const string TrainingComplete = "TRAINING_COMPLETE";
        public const string FirstSeederDestroyed = "FIRST_SEEDER_DESTROYED";
        public const string FirstPlanetCleared = "FIRST_PLANET_CLEARED";
        public const string DesertPlanetCleared = "DESERT_PLANET_CLEARED";
        public const string FirstMothershipDestroyed = "FIRST_MOTHERSHIP_DESTROYED";
        public const string CleanLoop = "CLEAN_LOOP";
        public const string LowAltitudeRun = "LOW_ALTITUDE_RUN";
        public const string DecoyKill = "DECOY_KILL";
        public const string SpeedUpgradeCollected = "SPEED_UPGRADE_COLLECTED";
        public const string NoDamagePlanetClear = "NO_DAMAGE_PLANET_CLEAR";
    }

    public static class Stats
    {
        public const string BestScore = "BEST_SCORE";
        public const string TotalScore = "TOTAL_SCORE";
        public const string TotalKills = "TOTAL_KILLS";
        public const string SeedersDestroyed = "SEEDERS_DESTROYED";
        public const string MothershipsDestroyed = "MOTHERSHIPS_DESTROYED";
        public const string PlanetsCleared = "PLANETS_CLEARED";
        public const string CleanLoops = "CLEAN_LOOPS";
        public const string LowAltitudeBonusPoints = "LOW_ALTITUDE_BONUS_POINTS";
        public const string DecoysDeployed = "DECOYS_DEPLOYED";
        public const string Deaths = "DEATHS";
        public const string TimePlayedSeconds = "TIME_PLAYED_SECONDS";
    }
}
