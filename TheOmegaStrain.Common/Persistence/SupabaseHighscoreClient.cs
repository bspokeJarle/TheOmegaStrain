using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace TheOmegaStrain.Common.Persistence
{
    /// <summary>
    /// Lightweight Supabase REST client for the highscores table.
    /// Uses the PostgREST API (no SDK dependency).
    ///
    /// Expected table schema (Supabase → SQL Editor):
    /// <code>
    /// CREATE TABLE highscores (
    ///     id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ///     player_name TEXT        NOT NULL,
    ///     score       BIGINT      NOT NULL,
    ///     wave_reached INT        NOT NULL DEFAULT 1,
    ///     total_kills  INT        NOT NULL DEFAULT 0,
    ///     total_shots_fired INT   NOT NULL DEFAULT 0,
    ///     total_deaths INT        NOT NULL DEFAULT 0,
    ///     accuracy     REAL       NOT NULL DEFAULT 0,
    ///     date_utc     TEXT       NOT NULL,
    ///     created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
    /// );
    ///
    /// -- Allow anonymous inserts and reads (RLS)
    /// ALTER TABLE highscores ENABLE ROW LEVEL SECURITY;
    /// CREATE POLICY "Anyone can read"  ON highscores FOR SELECT USING (true);
    /// CREATE POLICY "Anyone can insert" ON highscores FOR INSERT WITH CHECK (true);
    /// </code>
    /// </summary>
    public static class SupabaseHighscoreClient
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        // -----------------------------------------------------------------
        // Push a single entry to Supabase
        // -----------------------------------------------------------------

        /// <summary>
        /// Inserts a highscore entry into the remote table.
        /// Fire-and-forget safe — exceptions are swallowed.
        /// </summary>
        public static async Task PushEntryAsync(HighscoreEntry entry)
        {
            if (!PersistenceSetup.IsSupabaseConfigured) return;

            try
            {
                var url = $"{PersistenceSetup.SupabaseUrl}/rest/v1/{PersistenceSetup.SupabaseTableName}";

                var payload = new SupabaseHighscoreRow
                {
                    PlayerName = PlayerNameFormatter.Normalize(entry.PlayerName),
                    Score = entry.Score,
                    WaveReached = entry.WaveReached,
                    TotalKills = entry.TotalKills,
                    TotalShotsFired = entry.TotalShotsFired,
                    TotalDeaths = entry.TotalDeaths,
                    Accuracy = entry.Accuracy,
                    DateUtc = entry.DateUtc
                };

                var json = JsonSerializer.Serialize(payload, JsonOptions);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(json);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                SetSupabaseHeaders(request);

                // Prefer: return=minimal — don't need the inserted row back
                request.Headers.Add("Prefer", "return=minimal");

                using var response = await Http.SendAsync(request).ConfigureAwait(false);
                // Silently accept any result — local file is the source of truth
            }
            catch
            {
                // Offline or error — local file already has the entry
            }
        }

        // -----------------------------------------------------------------
        // Fetch top scores from Supabase
        // -----------------------------------------------------------------

        /// <summary>
        /// Fetches the top N highscores from the remote table, ordered by score descending.
        /// Returns null on failure.
        /// </summary>
        public static async Task<List<HighscoreEntry>?> FetchTopScoresAsync(int limit = 100)
        {
            if (!PersistenceSetup.IsSupabaseConfigured) return null;

            try
            {
                var url = $"{PersistenceSetup.SupabaseUrl}/rest/v1/{PersistenceSetup.SupabaseTableName}" +
                          $"?select=player_name,score,wave_reached,total_kills,total_shots_fired,total_deaths,accuracy,date_utc" +
                          $"&order=score.desc&limit={limit}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                SetSupabaseHeaders(request);

                using var response = await Http.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;

                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseHighscoreRows(body);
            }
            catch
            {
                return null;
            }
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static void SetSupabaseHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("apikey", PersistenceSetup.SupabaseAnonKey);
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", PersistenceSetup.SupabaseAnonKey);
        }

        internal static List<HighscoreEntry>? ParseHighscoreRows(string body)
        {
            var rows = JsonSerializer.Deserialize<List<SupabaseHighscoreRow>>(body, JsonOptions);
            if (rows == null) return null;

            var entries = new List<HighscoreEntry>(rows.Count);
            foreach (var row in rows)
            {
                entries.Add(new HighscoreEntry
                {
                    PlayerName = PlayerNameFormatter.Normalize(row.PlayerName),
                    Score = row.Score,
                    WaveReached = row.WaveReached,
                    TotalKills = row.TotalKills,
                    TotalShotsFired = row.TotalShotsFired,
                    TotalDeaths = row.TotalDeaths,
                    Accuracy = row.Accuracy,
                    DateUtc = row.DateUtc ?? ""
                });
            }

            return entries;
        }

        /// <summary>
        /// Internal DTO matching the Supabase/PostgREST snake_case column names.
        /// </summary>
        private sealed class SupabaseHighscoreRow
        {
            public string? PlayerName { get; set; }
            public long Score { get; set; }
            public int WaveReached { get; set; }
            public int TotalKills { get; set; }
            public int TotalShotsFired { get; set; }
            public int TotalDeaths { get; set; }
            public float Accuracy { get; set; }
            public string? DateUtc { get; set; }
        }
    }
}
