using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using System;
using System.IO;
using System.Text.Json;
using TheOmegaStrain.Common.CommonGlobalState.States;

namespace TheOmegaStrain.Common.Persistence
{
    /// <summary>
    /// Saves and restores the local game state as an encrypted JSON file.
    /// The file and its key are stored in the local data folder
    /// (<see cref="PersistenceSetup.LocalFolder"/>).
    /// </summary>
    public static class GameStatePersistence
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Writes the player's durable progress as an encrypted per-player file.
        /// When a checkpoint exists, the resumable state is the last checkpoint,
        /// not the volatile in-flight state at shutdown.
        /// </summary>
        public static void SaveGameState(bool allowScoreRollback = false)
        {
            var state = GameState.GamePlayState;
            if (string.IsNullOrWhiteSpace(state.PlayerName)) return;

            state.PlayerName = PlayerNameFormatter.Normalize(state.PlayerName);
            PlayerProgressService.ProtectAndApply(state);

            bool useCheckpoint = state.HasCheckpoint;
            if (!allowScoreRollback)
                ProtectScoreAgainstExistingSave(state, useCheckpoint);

            int checkpointSceneIndex = ResolveCheckpointSceneIndexForSave(state);
            int checkpointSimulationRound = ResolveCheckpointSimulationRoundForSave(state);
            SceneBiomeTypes checkpointSceneBiome = ResolveCheckpointSceneBiomeForSave(state);

            var saved = new SavedGameState
            {
                PlayerName = state.PlayerName,
                SceneIndex = useCheckpoint ? checkpointSceneIndex : state.SceneIndex,
                SimulationRound = useCheckpoint ? checkpointSimulationRound : state.SimulationRound,
                SceneBiome = useCheckpoint ? checkpointSceneBiome : state.CurrentSceneBiome,
                Score = useCheckpoint ? state.CheckpointScore : state.Score,
                PlanetStyleBonusScore = useCheckpoint ? state.CheckpointPlanetStyleBonusScore : state.PlanetStyleBonusScore,
                PlanetStyleBonusSceneIndex = useCheckpoint ? state.CheckpointPlanetStyleBonusSceneIndex : state.PlanetStyleBonusSceneIndex,
                Lives = useCheckpoint ? state.CheckpointLives : state.Lives,
                Health = useCheckpoint ? state.CheckpointHealth : state.Health,
                MaxHealth = state.MaxHealth,
                WaveNumber = useCheckpoint ? state.CheckpointWaveNumber : state.WaveNumber,
                PowerUpsCollected = useCheckpoint ? state.CheckpointPowerUpsCollected : state.PowerUpsCollected,
                SpeedPowerUpLevel = useCheckpoint ? state.CheckpointSpeedPowerUpLevel : state.SpeedPowerUpLevel,
                InfectionLevel = useCheckpoint ? state.CheckpointInfectionLevel : state.InfectionLevel,
                TotalBioTiles = state.TotalBioTiles,
                SeedersRemaining = useCheckpoint ? state.CheckpointSeedersRemaining : state.SeedersRemaining,
                DronesRemaining = useCheckpoint ? state.CheckpointDronesRemaining : state.DronesRemaining,
                MotherShipsRemaining = useCheckpoint ? state.CheckpointMotherShipsRemaining : state.MotherShipsRemaining,
                InitialSeeders = useCheckpoint ? state.CheckpointInitialSeeders : state.InitialSeeders,
                InitialDrones = useCheckpoint ? state.CheckpointInitialDrones : state.InitialDrones,
                InitialMotherShips = useCheckpoint ? state.CheckpointInitialMotherShips : state.InitialMotherShips,
                TotalShotsFired = useCheckpoint ? state.CheckpointTotalShotsFired : state.TotalShotsFired,
                TotalKills = useCheckpoint ? state.CheckpointTotalKills : state.TotalKills,
                TotalDeaths = useCheckpoint ? state.CheckpointTotalDeaths : state.TotalDeaths,
                HasCheckpoint = state.HasCheckpoint,
                CheckpointScore = state.CheckpointScore,
                CheckpointLives = state.CheckpointLives,
                CheckpointHealth = state.CheckpointHealth,
                CheckpointPowerUpsCollected = state.CheckpointPowerUpsCollected,
                CheckpointSpeedPowerUpLevel = state.CheckpointSpeedPowerUpLevel,
                CheckpointSeedersRemaining = state.CheckpointSeedersRemaining,
                CheckpointDronesRemaining = state.CheckpointDronesRemaining,
                CheckpointMotherShipsRemaining = state.CheckpointMotherShipsRemaining,
                CheckpointTotalShotsFired = state.CheckpointTotalShotsFired,
                CheckpointTotalKills = state.CheckpointTotalKills,
                CheckpointTotalDeaths = state.CheckpointTotalDeaths,
                CheckpointInfectionLevel = state.CheckpointInfectionLevel,
                CheckpointWaveNumber = state.CheckpointWaveNumber,
                CheckpointSceneIndex = useCheckpoint ? checkpointSceneIndex : state.CheckpointSceneIndex,
                CheckpointSimulationRound = useCheckpoint ? checkpointSimulationRound : state.CheckpointSimulationRound,
                CheckpointSceneBiome = useCheckpoint ? checkpointSceneBiome : state.CheckpointSceneBiome,
                CheckpointInitialSeeders = state.CheckpointInitialSeeders,
                CheckpointInitialDrones = state.CheckpointInitialDrones,
                CheckpointInitialMotherShips = state.CheckpointInitialMotherShips,
                CheckpointPlanetStyleBonusScore = state.CheckpointPlanetStyleBonusScore,
                CheckpointPlanetStyleBonusSceneIndex = state.CheckpointPlanetStyleBonusSceneIndex,
                HasPlanetStartSnapshot = state.HasPlanetStartSnapshot,
                PlanetStartSceneIndex = state.PlanetStartSceneIndex,
                PlanetStartScore = state.PlanetStartScore,
                PlanetStartLives = state.PlanetStartLives,
                PlanetStartHealth = state.PlanetStartHealth,
                PlanetStartPowerUpsCollected = state.PlanetStartPowerUpsCollected,
                PlanetStartSpeedPowerUpLevel = state.PlanetStartSpeedPowerUpLevel,
                PlanetStartSeedersRemaining = state.PlanetStartSeedersRemaining,
                PlanetStartDronesRemaining = state.PlanetStartDronesRemaining,
                PlanetStartMotherShipsRemaining = state.PlanetStartMotherShipsRemaining,
                PlanetStartTotalShotsFired = state.PlanetStartTotalShotsFired,
                PlanetStartTotalKills = state.PlanetStartTotalKills,
                PlanetStartTotalDeaths = state.PlanetStartTotalDeaths,
                PlanetStartInfectionLevel = state.PlanetStartInfectionLevel,
                PlanetStartWaveNumber = state.PlanetStartWaveNumber,
                PlanetStartSimulationRound = state.PlanetStartSimulationRound,
                PlanetStartSceneBiome = state.PlanetStartSceneBiome,
                PlanetStartInitialSeeders = state.PlanetStartInitialSeeders,
                PlanetStartInitialDrones = state.PlanetStartInitialDrones,
                PlanetStartInitialMotherShips = state.PlanetStartInitialMotherShips,
                PlanetStartPlanetStyleBonusScore = state.PlanetStartPlanetStyleBonusScore,
                PlanetStartPlanetStyleBonusSceneIndex = state.PlanetStartPlanetStyleBonusSceneIndex,
                SavedAtUtc = DateTime.UtcNow.ToString("o")
            };

            var filePath = PersistenceSetup.GetPlayerGameStateFilePath(state.PlayerName);
            Directory.CreateDirectory(PersistenceSetup.LocalFolder);
            EncryptionHelper.EnsureKeyFile(PersistenceSetup.LocalKeyFilePath);

            var json = JsonSerializer.Serialize(saved, JsonOptions);
            EncryptionHelper.EncryptToFileAtomic(
                filePath,
                PersistenceSetup.GetPlayerGameStateBackupFilePath(state.PlayerName),
                json,
                PersistenceSetup.LocalKeyFilePath);

            try { HighscoreService.SubmitFromGamePlay(state); } catch { }
        }

        private static int ResolveCheckpointSceneIndexForSave(GamePlayState state)
        {
            if (!state.HasCheckpoint)
                return state.SceneIndex;

            return state.CheckpointSceneIndex > 0
                ? state.CheckpointSceneIndex
                : state.SceneIndex;
        }

        private static int ResolveCheckpointSimulationRoundForSave(GamePlayState state)
        {
            if (!state.HasCheckpoint)
                return state.SimulationRound;

            return state.CheckpointSimulationRound != 0 || state.SimulationRound == 0
                ? state.CheckpointSimulationRound
                : state.SimulationRound;
        }

        private static SceneBiomeTypes ResolveCheckpointSceneBiomeForSave(GamePlayState state)
        {
            if (!state.HasCheckpoint)
                return state.CurrentSceneBiome;

            // Legacy checkpoints did not store biome. If the checkpoint still has the
            // default biome while the active scene has a concrete biome, keep the save coherent.
            if (state.CheckpointSimulationRound == 0 &&
                state.CheckpointSceneBiome == SceneBiomeTypes.HillsWoods &&
                state.CurrentSceneBiome != SceneBiomeTypes.HillsWoods)
            {
                return state.CurrentSceneBiome;
            }

            return state.CheckpointSceneBiome;
        }

        private static void ProtectScoreAgainstExistingSave(GamePlayState state, bool useCheckpoint)
        {
            var existing = LoadGameStateCore(state.PlayerName, repairHighscore: false);
            if (existing == null)
                return;

            long resumableScore = useCheckpoint ? state.CheckpointScore : state.Score;
            if (existing.Score <= resumableScore)
                return;

            state.Score = Math.Max(state.Score, existing.Score);
            if (useCheckpoint)
                state.CheckpointScore = existing.Score;
        }

        /// <summary>
        /// Loads the saved game state for a specific player from disk.
        /// Returns null if no save exists or if decryption fails.
        /// </summary>
        public static SavedGameState? LoadGameState(string playerName) =>
            LoadGameStateCore(playerName, repairHighscore: true);

        private static SavedGameState? LoadGameStateCore(string playerName, bool repairHighscore)
        {
            try
            {
                var normalizedName = PlayerNameFormatter.Normalize(playerName);
                var filePath = PersistenceSetup.GetPlayerGameStateFilePath(normalizedName);
                var backupFilePath = PersistenceSetup.GetPlayerGameStateBackupFilePath(normalizedName);
                if (!File.Exists(filePath) && !File.Exists(backupFilePath))
                    return null;

                var json = EncryptionHelper.DecryptFromFileOrBackup(
                    filePath,
                    backupFilePath,
                    PersistenceSetup.LocalKeyFilePath);

                if (json == null) return null;

                var saved = JsonSerializer.Deserialize<SavedGameState>(json, JsonOptions);
                if (saved != null)
                {
                    saved.PlayerName = string.IsNullOrWhiteSpace(saved.PlayerName)
                        ? normalizedName
                        : PlayerNameFormatter.Normalize(saved.PlayerName);
                    PlayerProgressService.ProtectAndApply(saved);
                    if (repairHighscore)
                    {
                        float accuracy = saved.TotalShotsFired > 0
                            ? (float)saved.TotalKills / saved.TotalShotsFired
                            : 0f;
                        HighscoreService.TrySubmitScore(
                            saved.PlayerName,
                            saved.Score,
                            saved.SceneIndex,
                            saved.TotalKills,
                            saved.TotalShotsFired,
                            saved.TotalDeaths,
                            accuracy);
                    }
                }
                return saved;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Applies a previously loaded save onto the live
        /// <see cref="GameState.GamePlayState"/>.
        /// </summary>
        public static void RestoreToGamePlayState(SavedGameState saved)
        {
            var state = GameState.GamePlayState;

            state.PlayerName = PlayerNameFormatter.Normalize(saved.PlayerName);
            state.Score = saved.Score;
            state.PlanetStyleBonusScore = saved.PlanetStyleBonusScore;
            state.PlanetStyleBonusSceneIndex = saved.PlanetStyleBonusSceneIndex;
            state.SceneIndex = saved.HasCheckpoint && saved.CheckpointSceneIndex > 0
                ? saved.CheckpointSceneIndex
                : saved.SceneIndex;
            state.SimulationRound = saved.SimulationRound;
            state.CurrentSceneBiome = saved.SceneBiome;
            state.Lives = saved.Lives;
            state.Health = saved.Health;
            state.MaxHealth = saved.MaxHealth;
            state.WaveNumber = saved.WaveNumber;
            state.PowerUpsCollected = saved.PowerUpsCollected;
            state.SpeedPowerUpLevel = saved.SpeedPowerUpLevel;
            state.InfectionLevel = saved.InfectionLevel;
            state.TotalBioTiles = saved.TotalBioTiles;
            state.SeedersRemaining = saved.SeedersRemaining;
            state.DronesRemaining = saved.DronesRemaining;
            state.MotherShipsRemaining = saved.MotherShipsRemaining;
            state.InitialSeeders = saved.InitialSeeders > 0 ? saved.InitialSeeders : saved.CheckpointInitialSeeders;
            state.InitialDrones = saved.InitialDrones > 0 ? saved.InitialDrones : saved.CheckpointInitialDrones;
            state.InitialMotherShips = saved.InitialMotherShips > 0 ? saved.InitialMotherShips : saved.CheckpointInitialMotherShips;
            state.TotalShotsFired = saved.TotalShotsFired;
            state.TotalKills = saved.TotalKills;
            state.TotalDeaths = saved.TotalDeaths;
            state.HasCheckpoint = saved.HasCheckpoint;
            state.CheckpointScore = saved.CheckpointScore;
            state.CheckpointLives = saved.CheckpointLives;
            state.CheckpointHealth = saved.CheckpointHealth;
            state.CheckpointPowerUpsCollected = saved.CheckpointPowerUpsCollected;
            state.CheckpointSpeedPowerUpLevel = saved.CheckpointSpeedPowerUpLevel;
            state.CheckpointSeedersRemaining = saved.CheckpointSeedersRemaining;
            state.CheckpointDronesRemaining = saved.CheckpointDronesRemaining;
            state.CheckpointMotherShipsRemaining = saved.CheckpointMotherShipsRemaining;
            state.CheckpointTotalShotsFired = saved.CheckpointTotalShotsFired;
            state.CheckpointTotalKills = saved.CheckpointTotalKills;
            state.CheckpointTotalDeaths = saved.CheckpointTotalDeaths;
            state.CheckpointInfectionLevel = saved.CheckpointInfectionLevel;
            state.CheckpointWaveNumber = saved.CheckpointWaveNumber;
            state.CheckpointSceneIndex = saved.CheckpointSceneIndex;
            state.CheckpointSimulationRound = saved.CheckpointSimulationRound;
            state.CheckpointSceneBiome = saved.CheckpointSceneBiome;
            state.CheckpointInitialSeeders = saved.CheckpointInitialSeeders;
            state.CheckpointInitialDrones = saved.CheckpointInitialDrones;
            state.CheckpointInitialMotherShips = saved.CheckpointInitialMotherShips;
            state.CheckpointPlanetStyleBonusScore = saved.CheckpointPlanetStyleBonusScore;
            state.CheckpointPlanetStyleBonusSceneIndex = saved.CheckpointPlanetStyleBonusSceneIndex;
            state.HasPlanetStartSnapshot = saved.HasPlanetStartSnapshot;
            state.PlanetStartSceneIndex = saved.PlanetStartSceneIndex;
            state.PlanetStartScore = saved.PlanetStartScore;
            state.PlanetStartLives = saved.PlanetStartLives;
            state.PlanetStartHealth = saved.PlanetStartHealth;
            state.PlanetStartPowerUpsCollected = saved.PlanetStartPowerUpsCollected;
            state.PlanetStartSpeedPowerUpLevel = saved.PlanetStartSpeedPowerUpLevel;
            state.PlanetStartSeedersRemaining = saved.PlanetStartSeedersRemaining;
            state.PlanetStartDronesRemaining = saved.PlanetStartDronesRemaining;
            state.PlanetStartMotherShipsRemaining = saved.PlanetStartMotherShipsRemaining;
            state.PlanetStartTotalShotsFired = saved.PlanetStartTotalShotsFired;
            state.PlanetStartTotalKills = saved.PlanetStartTotalKills;
            state.PlanetStartTotalDeaths = saved.PlanetStartTotalDeaths;
            state.PlanetStartInfectionLevel = saved.PlanetStartInfectionLevel;
            state.PlanetStartWaveNumber = saved.PlanetStartWaveNumber;
            state.PlanetStartSimulationRound = saved.PlanetStartSimulationRound;
            state.PlanetStartSceneBiome = saved.PlanetStartSceneBiome;
            state.PlanetStartInitialSeeders = saved.PlanetStartInitialSeeders;
            state.PlanetStartInitialDrones = saved.PlanetStartInitialDrones;
            state.PlanetStartInitialMotherShips = saved.PlanetStartInitialMotherShips;
            state.PlanetStartPlanetStyleBonusScore = saved.PlanetStartPlanetStyleBonusScore;
            state.PlanetStartPlanetStyleBonusSceneIndex = saved.PlanetStartPlanetStyleBonusSceneIndex;
        }

        /// <summary>
        /// Returns true if a saved game file exists for the given player.
        /// </summary>
        public static bool HasSavedGame(string playerName) =>
            PersistenceSetup.HasPlayerSaveFile(PlayerNameFormatter.Normalize(playerName));

        /// <summary>
        /// Deletes the saved game file for the given player.
        /// </summary>
        public static void DeleteSave(string playerName)
        {
            var normalizedName = PlayerNameFormatter.Normalize(playerName);
            var path = PersistenceSetup.GetPlayerGameStateFilePath(normalizedName);
            if (File.Exists(path))
                File.Delete(path);
            var backupPath = PersistenceSetup.GetPlayerGameStateBackupFilePath(normalizedName);
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }

        /// <summary>
        /// Resets a player's save to Scene1 with a clean progression state.
        /// Use this when a player should restart campaign flow from the first game scene.
        /// </summary>
        public static void ResetPlayerToScene1(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return;

            var normalizedName = PlayerNameFormatter.Normalize(playerName);
            var saved = LoadGameState(normalizedName) ?? new SavedGameState
            {
                PlayerName = normalizedName,
                Lives = 3,
                Health = 100f,
                MaxHealth = 100f,
                SavedAtUtc = DateTime.UtcNow.ToString("o")
            };

            saved.PlayerName = normalizedName;
            saved.SceneIndex = 1;
            saved.SceneBiome = TheOmegaStrain.Domain.SceneBiomeTypes.HillsWoods;
            saved.Score = 0;
            saved.PlanetStyleBonusScore = 0;
            saved.PlanetStyleBonusSceneIndex = 1;
            saved.WaveNumber = 1;
            saved.PowerUpsCollected = 0;
            saved.SpeedPowerUpLevel = 0;
            saved.InfectionLevel = 0f;
            saved.TotalBioTiles = 0;
            saved.TotalShotsFired = 0;
            saved.TotalKills = 0;
            saved.TotalDeaths = 0;
            saved.Lives = 3;
            saved.Health = 100f;
            saved.MaxHealth = 100f;
            saved.HasCheckpoint = false;
            saved.SeedersRemaining = 0;
            saved.DronesRemaining = 0;
            saved.MotherShipsRemaining = 0;
            saved.CheckpointScore = 0;
            saved.CheckpointLives = 3;
            saved.CheckpointHealth = 100f;
            saved.CheckpointPowerUpsCollected = 0;
            saved.CheckpointSpeedPowerUpLevel = 0;
            saved.CheckpointSeedersRemaining = 0;
            saved.CheckpointDronesRemaining = 0;
            saved.CheckpointMotherShipsRemaining = 0;
            saved.CheckpointTotalShotsFired = 0;
            saved.CheckpointTotalKills = 0;
            saved.CheckpointTotalDeaths = 0;
            saved.CheckpointInfectionLevel = 0f;
            saved.CheckpointWaveNumber = 1;
            saved.CheckpointSceneIndex = 0;
            saved.CheckpointInitialSeeders = 0;
            saved.CheckpointInitialDrones = 0;
            saved.CheckpointInitialMotherShips = 0;
            saved.CheckpointPlanetStyleBonusScore = 0;
            saved.CheckpointPlanetStyleBonusSceneIndex = 1;
            saved.HasPlanetStartSnapshot = false;
            saved.PlanetStartSceneIndex = 1;
            saved.PlanetStartScore = 0;
            saved.PlanetStartLives = 3;
            saved.PlanetStartHealth = 100f;
            saved.PlanetStartPowerUpsCollected = 0;
            saved.PlanetStartSpeedPowerUpLevel = 0;
            saved.PlanetStartSeedersRemaining = 0;
            saved.PlanetStartDronesRemaining = 0;
            saved.PlanetStartMotherShipsRemaining = 0;
            saved.PlanetStartTotalShotsFired = 0;
            saved.PlanetStartTotalKills = 0;
            saved.PlanetStartTotalDeaths = 0;
            saved.PlanetStartInfectionLevel = 0f;
            saved.PlanetStartWaveNumber = 1;
            saved.PlanetStartInitialSeeders = 0;
            saved.PlanetStartInitialDrones = 0;
            saved.PlanetStartInitialMotherShips = 0;
            saved.PlanetStartPlanetStyleBonusScore = 0;
            saved.PlanetStartPlanetStyleBonusSceneIndex = 1;
            saved.SavedAtUtc = DateTime.UtcNow.ToString("o");
            PlayerProgressService.ProtectAndApply(saved);

            var filePath = PersistenceSetup.GetPlayerGameStateFilePath(normalizedName);
            Directory.CreateDirectory(PersistenceSetup.LocalFolder);
            EncryptionHelper.EnsureKeyFile(PersistenceSetup.LocalKeyFilePath);
            var json = JsonSerializer.Serialize(saved, JsonOptions);
            EncryptionHelper.EncryptToFileAtomic(
                filePath,
                PersistenceSetup.GetPlayerGameStateBackupFilePath(normalizedName),
                json,
                PersistenceSetup.LocalKeyFilePath);

            // If this player is active in-memory, reset runtime state as well so
            // scene progression does not keep stale values until next restart.
            var state = GameState.GamePlayState;
            if (string.Equals(state.PlayerName, normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                state.PlayerName = normalizedName;
                state.SceneIndex = 1;
                state.CurrentSceneBiome = TheOmegaStrain.Domain.SceneBiomeTypes.HillsWoods;
                state.Score = 0;
                state.PlanetStyleBonusScore = 0;
                state.PlanetStyleBonusSceneIndex = 1;
                state.WaveNumber = 1;
                state.PowerUpsCollected = 0;
                state.SpeedPowerUpLevel = 0;
                state.InfectionLevel = 0f;
                state.TotalBioTiles = 0;
                state.TotalShotsFired = 0;
                state.TotalKills = 0;
                state.TotalDeaths = 0;
                state.Lives = 3;
                state.Health = 100f;
                state.MaxHealth = 100f;
                state.ClearCheckpoint();
                state.ClearPlanetStartSnapshot();
                PlayerProgressService.ApplyDurableProgress(state);
            }
        }
    }
}
