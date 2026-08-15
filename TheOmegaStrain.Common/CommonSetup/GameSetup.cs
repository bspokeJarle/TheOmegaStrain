namespace TheOmegaStrain.Common.CommonSetup
{
    public static class GameSetup
    {
        public static float CollisionMarginX { get; set; } = 10f;
        public static float CollisionMarginY { get; set; } = 10f;
        public static float CollisionMarginZ { get; set; } = 20f;
        public static float MaxKamikazeShipCenterCollisionDistance { get; set; } = 150f;
        public static int MaxActiveDecoys { get; set; } = 3;
        public static int ShipShadowSubdivisionLevels { get; set; } = 2;

        // Kamikaze drone hunt timing
        public static int KamikazeDroneMinHuntDelay { get; set; } = 5;
        public static int KamikazeDroneMaxHuntDelay { get; set; } = 45;
        public static float KamikazeDroneProximityHuntDistance { get; set; } = 10_000f;
        public static int KamikazeDroneSurpriseDelayPercent { get; set; } = 10;
        public static int KamikazeDroneSurpriseHuntDelaySeconds { get; set; } = 12;

        // Decoy blast radius: exploding decoys damage nearby objects within this distance.
        // Viewport diagonal is ~960 world units; 850 covers most of the visible screen.
        public static float DecoyBlastRadius { get; set; } = 850f;

        // BomberBomb blast radius: exploding bombs damage the ship within this distance.
        public static float BomberBombBlastRadius { get; set; } = 600f;
        public static float BomberBombBlastDamage { get; set; } = 35f;

        // Scoring: base points awarded per enemy kill
        public static int SeederKillScore { get; set; } = 100;
        public static int KamikazeDroneKillScore { get; set; } = 50;
        public static int MotherShipSmallKillScore { get; set; } = 500;
        public static int SpaceSwanKillScore { get; set; } = 50;
        public static int ZeppelinBomberKillScore { get; set; } = 75;
        public static int DefaultKillScore { get; set; } = 25;
        public static int PowerUpCollectScore { get; set; } = 150;
        public static int PlanetStyleBonusScoreCap { get; set; } = 2000;
        public static int CleanLoopStyleBonusScore { get; set; } = 250;
        public static int CollisionLoopStyleBonusScore { get; set; } = 0;
        public static int LowAltitudeRunStyleBonusScore { get; set; } = 250;
        public static float LowAltitudeRunMinHeight { get; set; } = 20f;
        public static float LowAltitudeRunMaxHeight { get; set; } = 126f;
        public static float LowAltitudeRunMinHorizontalSpeed { get; set; } = 5f;
        public static float LowAltitudeRunRequiredSeconds { get; set; } = 4.6f;
        public static int PlanetBiomassContainedPointsPerCleanPercent { get; set; } = 10;
        public static int PlanetHullIntegrityMaxBonus { get; set; } = 800;
        public static int PlanetLifePreservedBonus { get; set; } = 500;
        public static int PlanetDeathlessBonus { get; set; } = 750;
        public static int PlanetMothershipTakedownBonusPerScene { get; set; } = 250;
        public static int PlanetPrecisionBonusTier1 { get; set; } = 250;
        public static int PlanetPrecisionBonusTier2 { get; set; } = 500;
        public static int PlanetPrecisionBonusTier3 { get; set; } = 1000;

        // Score penalty deducted each time the player dies
        public static int DeathScorePenalty { get; set; } = 200;

        // End-of-game accuracy bonus: finalBonus = baseKillScore * accuracy * this multiplier
        public static float AccuracyBonusMultiplier { get; set; } = 2.0f;

        /// <summary>
        /// Returns the score awarded for killing an enemy of the given type.
        /// </summary>
        public static int GetKillScore(string enemyType) => enemyType switch
        {
            "Seeder" => SeederKillScore,
            "KamikazeDrone" => KamikazeDroneKillScore,
            "MotherShipSmall" => MotherShipSmallKillScore,
            "SpaceSwan" => SpaceSwanKillScore,
            "ZeppelinBomber" => ZeppelinBomberKillScore,
            _ => DefaultKillScore
        };

        /// <summary>
        /// Returns true if the killed enemy should trigger a checkpoint auto-save.
        /// Checkpoints are tied to MotherShip kills only. Powerups produce a checkpoint
        /// when the player actually picks them up (handled in ShipControls.CollectPowerUp),
        /// not the moment a seeder drops one.
        /// </summary>
        public static bool IsCheckpointEnemy(string objectName, bool hasPowerUp)
        {
            if (objectName.StartsWith("MotherShip")) return true;
            return false;
        }
    }
}
