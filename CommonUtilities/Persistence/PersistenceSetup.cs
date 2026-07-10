using System;
using System.IO;
using System.Text.Json;

namespace CommonUtilities.Persistence
{
    /// <summary>
    /// Configuration for encrypted game state and highscore persistence.
    /// Local game state and highscores are stored in %APPDATA%\OmegaStrain.
    /// Highscores are also synced to a Supabase Postgres database when online.
    /// Each folder contains its own encryption key file so the key is never in source code.
    /// </summary>
    public static class PersistenceSetup
    {
        private const string AppFolderName = "OmegaStrain";

        public static string KeyFileName { get; set; } = "omega.key";
        public static string HighscoreFileName { get; set; } = "highscores.enc";
        public static int MaxHighscoreEntries { get; set; } = 100;

        // -----------------------------------------------------------------
        // Supabase throttle — limits remote API calls
        // -----------------------------------------------------------------

        /// <summary>
        /// Minimum interval between remote fetches. Default: 10 minutes.
        /// GetTopScores will return cached data if called again within this window.
        /// </summary>
        public static TimeSpan RemoteFetchCooldown { get; set; } = TimeSpan.FromMinutes(10);

        // -----------------------------------------------------------------
        // Local data folder (game state + local highscores)
        // -----------------------------------------------------------------
        private static string? _localFolder;
        public static string LocalFolder
        {
            get => _localFolder ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppFolderName);
            set => _localFolder = value;
        }

        public static string LocalKeyFilePath => Path.Combine(LocalFolder, KeyFileName);
        public static string LocalHighscoreFilePath => Path.Combine(LocalFolder, HighscoreFileName);
        public static string LocalHighscoreBackupFilePath => LocalHighscoreFilePath + ".bak";
        public static string LocalSettingsFilePath => Path.Combine(LocalFolder, "settings.json");

        /// <summary>
        /// Returns the per-player save file path for the given player name.
        /// The name is sanitised to a safe filename.
        /// </summary>
        public static string GetPlayerGameStateFilePath(string playerName)
        {
            var safe = SanitiseFileName(playerName);
            return Path.Combine(LocalFolder, $"save_{safe}.enc");
        }

        public static string GetPlayerGameStateBackupFilePath(string playerName)
            => GetPlayerGameStateFilePath(playerName) + ".bak";

        public static string GetPlayerProgressFilePath(string playerName)
        {
            var safe = SanitiseFileName(playerName);
            return Path.Combine(LocalFolder, $"progress_{safe}.enc");
        }

        public static string GetPlayerProgressBackupFilePath(string playerName)
            => GetPlayerProgressFilePath(playerName) + ".bak";

        /// <summary>
        /// Returns the per-player tutorial progress file path for the given player name.
        /// </summary>
        public static string GetPlayerTutorialProgressFilePath(string playerName)
        {
            var safe = SanitiseFileName(playerName);
            return Path.Combine(LocalFolder, $"tutorial_{safe}.json");
        }

        /// <summary>
        /// Returns true if a per-player save file exists for the given name.
        /// </summary>
        public static bool HasPlayerSaveFile(string playerName)
            => File.Exists(GetPlayerGameStateFilePath(playerName)) ||
               File.Exists(GetPlayerGameStateBackupFilePath(playerName));

        private static string SanitiseFileName(string name)
        {
            var lower = name.Trim().ToLowerInvariant();
            var invalid = Path.GetInvalidFileNameChars();
            var chars = lower.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == ' ')
                    chars[i] = '_';
            }
            var result = new string(chars);
            return string.IsNullOrEmpty(result) ? "default" : result;
        }

        /// <summary>
        /// Path to the plain-text file storing the last used player name.
        /// </summary>
        public static string LastPlayerNameFilePath => Path.Combine(LocalFolder, "lastplayer.txt");

        /// <summary>
        /// Loads the last used player name, or empty string if none saved.
        /// </summary>
        public static string LoadLastPlayerName()
        {
            try
            {
                return File.Exists(LastPlayerNameFilePath)
                    ? PlayerNameFormatter.Normalize(File.ReadAllText(LastPlayerNameFilePath))
                    : "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// Saves the player name so it can be pre-filled next session.
        /// </summary>
        public static void SaveLastPlayerName(string name)
        {
            try { File.WriteAllText(LastPlayerNameFilePath, PlayerNameFormatter.Normalize(name)); }
            catch { }
        }

        // -----------------------------------------------------------------
        // Supabase configuration (connection details set at startup)
        // -----------------------------------------------------------------

        /// <summary>
        /// Supabase project URL, e.g. "https://xxxxx.supabase.co".
        /// Set before calling Initialize() or leave null to run offline-only.
        /// </summary>
        public static string? SupabaseUrl { get; set; }

        /// <summary>
        /// Supabase anon/public API key (safe to embed — RLS protects the data).
        /// </summary>
        public static string? SupabaseAnonKey { get; set; }

        /// <summary>
        /// Name of the highscores table in Supabase. Default: "highscores".
        /// </summary>
        public static string SupabaseTableName { get; set; } = "highscores";

        /// <summary>
        /// True when both URL and key are configured.
        /// </summary>
        public static bool IsSupabaseConfigured =>
            !string.IsNullOrWhiteSpace(SupabaseUrl) &&
            !string.IsNullOrWhiteSpace(SupabaseAnonKey);

        // -----------------------------------------------------------------
        // Initialization
        // -----------------------------------------------------------------

        private const string SecretsFileName = "secrets.json";

        /// <summary>
        /// Full path to the local secrets file in the app data folder.
        /// </summary>
        public static string SecretsFilePath => Path.Combine(LocalFolder, SecretsFileName);

        /// <summary>
        /// Creates local data folder, loads secrets, and ensures key files exist.
        /// Call once at application startup.
        /// </summary>
        public static void Initialize()
        {
            Directory.CreateDirectory(LocalFolder);
            LoadSecrets();
            EncryptionHelper.EnsureKeyFile(LocalKeyFilePath);
        }

        /// <summary>
        /// Reads Supabase credentials from the local secrets.json file.
        /// If the file does not exist, the app runs in offline-only mode.
        /// The file is stored in %APPDATA%/OmegaStrain and is never committed to source control.
        /// </summary>
        private static void LoadSecrets()
        {
            try
            {
                if (!File.Exists(SecretsFilePath)) return;

                var json = File.ReadAllText(SecretsFilePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("SupabaseUrl", out var urlProp))
                    SupabaseUrl = urlProp.GetString();

                if (root.TryGetProperty("SupabaseAnonKey", out var keyProp))
                    SupabaseAnonKey = keyProp.GetString();
            }
            catch
            {
                // Secrets file missing or malformed — run offline
            }
        }
    }
}
