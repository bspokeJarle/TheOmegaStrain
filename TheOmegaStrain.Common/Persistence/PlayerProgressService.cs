using TheOmegaStrain.Domain;
using System;
using System.IO;
using System.Text.Json;
using TheOmegaStrain.Common.CommonGlobalState.States;

namespace TheOmegaStrain.Common.Persistence
{
    public sealed class PlayerProgressState
    {
        public string PlayerName { get; set; } = "";
        public int PowerUpsCollected { get; set; }
        public int SpeedPowerUpLevel { get; set; }
        public string SavedAtUtc { get; set; } = "";
    }

    public static class PlayerProgressService
    {
        private static readonly object Gate = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static PlayerProgressState? Load(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return null;

            lock (Gate)
            {
                return LoadCore(PlayerNameFormatter.Normalize(playerName));
            }
        }

        public static PlayerProgressState ProtectAndApply(GamePlayState state)
        {
            lock (Gate)
            {
                int powerUps = state.PowerUpsCollected;
                int speedLevel = state.SpeedPowerUpLevel;
                if (state.HasCheckpoint)
                {
                    powerUps = Math.Max(powerUps, state.CheckpointPowerUpsCollected);
                    speedLevel = Math.Max(speedLevel, state.CheckpointSpeedPowerUpLevel);
                }
                if (state.HasPlanetStartSnapshot)
                {
                    powerUps = Math.Max(powerUps, state.PlanetStartPowerUpsCollected);
                    speedLevel = Math.Max(speedLevel, state.PlanetStartSpeedPowerUpLevel);
                }

                state.PlayerName = PlayerNameFormatter.Normalize(state.PlayerName);
                var progress = ProtectAndSaveCore(state.PlayerName, powerUps, speedLevel);
                ApplyToGamePlayState(state, progress);
                return progress;
            }
        }

        public static void ApplyDurableProgress(GamePlayState state)
        {
            lock (Gate)
            {
                state.PlayerName = PlayerNameFormatter.Normalize(state.PlayerName);
                var progress = LoadCore(state.PlayerName);
                if (progress != null)
                    ApplyToGamePlayState(state, progress);
            }
        }

        public static void ProtectAndApply(SavedGameState saved)
        {
            lock (Gate)
            {
                int powerUps = saved.PowerUpsCollected;
                int speedLevel = saved.SpeedPowerUpLevel;
                if (saved.HasCheckpoint)
                {
                    powerUps = Math.Max(powerUps, saved.CheckpointPowerUpsCollected);
                    speedLevel = Math.Max(speedLevel, saved.CheckpointSpeedPowerUpLevel);
                }
                if (saved.HasPlanetStartSnapshot)
                {
                    powerUps = Math.Max(powerUps, saved.PlanetStartPowerUpsCollected);
                    speedLevel = Math.Max(speedLevel, saved.PlanetStartSpeedPowerUpLevel);
                }

                saved.PlayerName = PlayerNameFormatter.Normalize(saved.PlayerName);
                var progress = ProtectAndSaveCore(saved.PlayerName, powerUps, speedLevel);
                saved.PowerUpsCollected = Math.Max(saved.PowerUpsCollected, progress.PowerUpsCollected);
                saved.CheckpointPowerUpsCollected = Math.Max(saved.CheckpointPowerUpsCollected, progress.PowerUpsCollected);
                saved.PlanetStartPowerUpsCollected = Math.Max(saved.PlanetStartPowerUpsCollected, progress.PowerUpsCollected);
                saved.SpeedPowerUpLevel = Math.Max(saved.SpeedPowerUpLevel, progress.SpeedPowerUpLevel);
                saved.CheckpointSpeedPowerUpLevel = Math.Max(saved.CheckpointSpeedPowerUpLevel, progress.SpeedPowerUpLevel);
                saved.PlanetStartSpeedPowerUpLevel = Math.Max(saved.PlanetStartSpeedPowerUpLevel, progress.SpeedPowerUpLevel);
            }
        }

        private static PlayerProgressState ProtectAndSaveCore(
            string playerName,
            int powerUpsCollected,
            int speedPowerUpLevel)
        {
            var normalizedName = PlayerNameFormatter.Normalize(playerName);
            var existing = LoadCore(normalizedName);
            var progress = new PlayerProgressState
            {
                PlayerName = normalizedName,
                PowerUpsCollected = Math.Max(existing?.PowerUpsCollected ?? 0, Math.Max(0, powerUpsCollected)),
                SpeedPowerUpLevel = Math.Max(existing?.SpeedPowerUpLevel ?? 0, Math.Clamp(speedPowerUpLevel, 0, 2)),
                SavedAtUtc = DateTime.UtcNow.ToString("o")
            };

            Directory.CreateDirectory(PersistenceSetup.LocalFolder);
            EncryptionHelper.EnsureKeyFile(PersistenceSetup.LocalKeyFilePath);
            string json = JsonSerializer.Serialize(progress, JsonOptions);
            EncryptionHelper.EncryptToFileAtomic(
                PersistenceSetup.GetPlayerProgressFilePath(normalizedName),
                PersistenceSetup.GetPlayerProgressBackupFilePath(normalizedName),
                json,
                PersistenceSetup.LocalKeyFilePath);
            return progress;
        }

        private static PlayerProgressState? LoadCore(string playerName)
        {
            try
            {
                var normalizedName = PlayerNameFormatter.Normalize(playerName);
                string? json = EncryptionHelper.DecryptFromFileOrBackup(
                    PersistenceSetup.GetPlayerProgressFilePath(normalizedName),
                    PersistenceSetup.GetPlayerProgressBackupFilePath(normalizedName),
                    PersistenceSetup.LocalKeyFilePath);
                var progress = json == null
                    ? null
                    : JsonSerializer.Deserialize<PlayerProgressState>(json, JsonOptions);
                if (progress != null)
                    progress.PlayerName = PlayerNameFormatter.Normalize(progress.PlayerName);
                return progress;
            }
            catch
            {
                return null;
            }
        }

        private static void ApplyToGamePlayState(GamePlayState state, PlayerProgressState progress)
        {
            state.PowerUpsCollected = Math.Max(state.PowerUpsCollected, progress.PowerUpsCollected);
            state.CheckpointPowerUpsCollected = Math.Max(state.CheckpointPowerUpsCollected, progress.PowerUpsCollected);
            state.PlanetStartPowerUpsCollected = Math.Max(state.PlanetStartPowerUpsCollected, progress.PowerUpsCollected);
            state.SpeedPowerUpLevel = Math.Max(state.SpeedPowerUpLevel, progress.SpeedPowerUpLevel);
            state.CheckpointSpeedPowerUpLevel = Math.Max(state.CheckpointSpeedPowerUpLevel, progress.SpeedPowerUpLevel);
            state.PlanetStartSpeedPowerUpLevel = Math.Max(state.PlanetStartSpeedPowerUpLevel, progress.SpeedPowerUpLevel);
        }
    }
}
