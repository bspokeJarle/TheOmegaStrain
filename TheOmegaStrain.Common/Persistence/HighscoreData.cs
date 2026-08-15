using System.Collections.Generic;

namespace CommonUtilities.Persistence
{
    /// <summary>
    /// A single highscore entry. Stored locally encrypted and synced to Supabase.
    /// </summary>
    public sealed class HighscoreEntry
    {
        public string PlayerName { get; set; } = "";
        public long Score { get; set; }
        public int WaveReached { get; set; }
        public int TotalKills { get; set; }
        public int TotalShotsFired { get; set; }
        public int TotalDeaths { get; set; }
        public float Accuracy { get; set; }
        public string DateUtc { get; set; } = "";
    }

    /// <summary>
    /// Container for the top 100 highscores. Serialized as encrypted JSON.
    /// </summary>
    public sealed class HighscoreList
    {
        public List<HighscoreEntry> Entries { get; set; } = new();
    }
}
