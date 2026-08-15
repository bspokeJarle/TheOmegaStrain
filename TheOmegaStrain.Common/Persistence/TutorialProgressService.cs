using System;
using System.IO;
using System.Text.Json;

namespace TheOmegaStrain.Common.Persistence
{
    /// <summary>
    /// Stores local per-player tutorial completion independently from campaign saves.
    /// </summary>
    public static class TutorialProgressService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static bool HasCompletedTutorial(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return false;

            try
            {
                var filePath = PersistenceSetup.GetPlayerTutorialProgressFilePath(PlayerNameFormatter.Normalize(playerName));
                if (!File.Exists(filePath))
                    return false;

                var json = File.ReadAllText(filePath);
                var state = JsonSerializer.Deserialize<TutorialProgressState>(json, JsonOptions);
                return state?.Completed == true;
            }
            catch
            {
                return false;
            }
        }

        public static void MarkTutorialCompleted(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return;

            try
            {
                var normalizedName = PlayerNameFormatter.Normalize(playerName);
                Directory.CreateDirectory(PersistenceSetup.LocalFolder);
                var state = new TutorialProgressState
                {
                    Completed = true,
                    CompletedAtUtc = DateTime.UtcNow.ToString("o")
                };

                var json = JsonSerializer.Serialize(state, JsonOptions);
                File.WriteAllText(PersistenceSetup.GetPlayerTutorialProgressFilePath(normalizedName), json);
            }
            catch
            {
            }
        }

        private sealed class TutorialProgressState
        {
            public bool Completed { get; set; }
            public string CompletedAtUtc { get; set; } = "";
        }
    }
}
