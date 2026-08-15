using RetroMesh.Engine;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using System;
using System.IO;
using System.Text.Json;

namespace TheOmegaStrain.Common.Persistence
{
    public static class GameSettingsPersistence
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public static GameSettingsState LoadSettings()
        {
            try
            {
                if (!File.Exists(PersistenceSetup.LocalSettingsFilePath))
                    return CreateDefaultSettings();

                var json = File.ReadAllText(PersistenceSetup.LocalSettingsFilePath);
                var state = JsonSerializer.Deserialize<GameSettingsState>(json, JsonOptions) ?? CreateDefaultSettings();
                MigrateLegacySettings(state, GetSettingsSchemaVersion(json));
                state.Normalize();
                return state;
            }
            catch
            {
                return CreateDefaultSettings();
            }
        }

        public static void LoadIntoGameState()
        {
            GameState.SettingsState = LoadSettings();
        }

        public static void SaveSettings(GameSettingsState settings)
        {
            if (settings == null)
                return;

            try
            {
                Directory.CreateDirectory(PersistenceSetup.LocalFolder);
                settings.Normalize();
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(PersistenceSetup.LocalSettingsFilePath, json);
            }
            catch
            {
            }
        }

        private static GameSettingsState CreateDefaultSettings()
        {
            var settings = new GameSettingsState();
            settings.Normalize();
            return settings;
        }

        private static void MigrateLegacySettings(GameSettingsState state, int settingsSchemaVersion)
        {
            if (settingsSchemaVersion < 2 &&
                state.XboxThrustButton == XboxControlButton.A &&
                state.XboxFireButton == XboxControlButton.RightTrigger)
            {
                state.XboxThrustButton = XboxControlButton.RightTrigger;
                state.XboxFireButton = XboxControlButton.LeftTrigger;
            }

            if (settingsSchemaVersion < 3 &&
                state.XboxBulletButton == XboxControlButton.X &&
                state.XboxDecoyButton == XboxControlButton.B &&
                state.XboxLazerButton == XboxControlButton.Y)
            {
                state.XboxDecoyButton = XboxControlButton.Y;
                state.XboxLazerButton = XboxControlButton.B;
                state.XboxPowerup4Button = XboxControlButton.A;
            }
        }

        private static int GetSettingsSchemaVersion(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("settingsSchemaVersion", out var version) &&
                    version.TryGetInt32(out int parsedVersion))
                {
                    return parsedVersion;
                }
            }
            catch
            {
            }

            return 0;
        }
    }
}
