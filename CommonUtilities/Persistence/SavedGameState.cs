namespace CommonUtilities.Persistence
{
    using Domain;

    /// <summary>
    /// Serializable snapshot of the player's game progress.
    /// Only the fields that matter for save/restore are included;
    /// runtime-only state (cooldowns, screen positions) is excluded.
    /// </summary>
    public sealed class SavedGameState
    {
        public string PlayerName { get; set; } = "";
        public int SceneIndex { get; set; }
        public int SimulationRound { get; set; }
        public SceneBiomeTypes SceneBiome { get; set; } = SceneBiomeTypes.HillsWoods;
        public long Score { get; set; }
        public int PlanetStyleBonusScore { get; set; }
        public int PlanetStyleBonusSceneIndex { get; set; }
        public int Lives { get; set; }
        public float Health { get; set; }
        public float MaxHealth { get; set; }
        public int WaveNumber { get; set; }
        public int PowerUpsCollected { get; set; }
        public int SpeedPowerUpLevel { get; set; }
        public float InfectionLevel { get; set; }
        public int TotalBioTiles { get; set; }
        public int SeedersRemaining { get; set; }
        public int DronesRemaining { get; set; }
        public int MotherShipsRemaining { get; set; }
        public int InitialSeeders { get; set; }
        public int InitialDrones { get; set; }
        public int InitialMotherShips { get; set; }

        // Combat statistics
        public int TotalShotsFired { get; set; }
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }

        // Checkpoint state
        public bool HasCheckpoint { get; set; }
        public long CheckpointScore { get; set; }
        public int CheckpointLives { get; set; }
        public float CheckpointHealth { get; set; }
        public int CheckpointPowerUpsCollected { get; set; }
        public int CheckpointSpeedPowerUpLevel { get; set; }
        public int CheckpointSeedersRemaining { get; set; }
        public int CheckpointDronesRemaining { get; set; }
        public int CheckpointMotherShipsRemaining { get; set; }
        public int CheckpointTotalShotsFired { get; set; }
        public int CheckpointTotalKills { get; set; }
        public int CheckpointTotalDeaths { get; set; }
        public float CheckpointInfectionLevel { get; set; }
        public int CheckpointWaveNumber { get; set; }
        public int CheckpointSceneIndex { get; set; }
        public int CheckpointSimulationRound { get; set; }
        public SceneBiomeTypes CheckpointSceneBiome { get; set; } = SceneBiomeTypes.HillsWoods;
        public int CheckpointInitialSeeders { get; set; }
        public int CheckpointInitialDrones { get; set; }
        public int CheckpointInitialMotherShips { get; set; }
        public int CheckpointPlanetStyleBonusScore { get; set; }
        public int CheckpointPlanetStyleBonusSceneIndex { get; set; }

        // Scene-start rollback state. Used when a restored checkpoint is mathematically unrecoverable.
        public bool HasPlanetStartSnapshot { get; set; }
        public int PlanetStartSceneIndex { get; set; }
        public long PlanetStartScore { get; set; }
        public int PlanetStartLives { get; set; }
        public float PlanetStartHealth { get; set; }
        public int PlanetStartPowerUpsCollected { get; set; }
        public int PlanetStartSpeedPowerUpLevel { get; set; }
        public int PlanetStartSeedersRemaining { get; set; }
        public int PlanetStartDronesRemaining { get; set; }
        public int PlanetStartMotherShipsRemaining { get; set; }
        public int PlanetStartTotalShotsFired { get; set; }
        public int PlanetStartTotalKills { get; set; }
        public int PlanetStartTotalDeaths { get; set; }
        public float PlanetStartInfectionLevel { get; set; }
        public int PlanetStartWaveNumber { get; set; }
        public int PlanetStartSimulationRound { get; set; }
        public SceneBiomeTypes PlanetStartSceneBiome { get; set; } = SceneBiomeTypes.HillsWoods;
        public int PlanetStartInitialSeeders { get; set; }
        public int PlanetStartInitialDrones { get; set; }
        public int PlanetStartInitialMotherShips { get; set; }
        public int PlanetStartPlanetStyleBonusScore { get; set; }
        public int PlanetStartPlanetStyleBonusSceneIndex { get; set; }

        public string SavedAtUtc { get; set; } = "";
    }
}
