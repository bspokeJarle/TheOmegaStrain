using Domain;
using System.Linq;
using System.Text;

namespace CommonUtilities.Persistence
{
    public static class HighscoreOverlayFormatter
    {
        private const string IntroHighscoreTitle = "TOP PILOTS";
        private const string OutroHighscoreTitle = "LEADERBOARD";

        public static string BuildBody(int count = 25)
        {
            var entries = HighscoreService.GetTopScores(count)
                .OrderByDescending(e => e.Score)
                .Take(count)
                .ToList();

            if (entries.Count == 0)
                return "No highscores recorded yet.\n\nBe the first pilot to make history!";

            var sb = new StringBuilder();
            sb.AppendLine("RANK  PILOT             SCORE      KILLS");
            sb.AppendLine("----  -----             -----      -----");

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var playerName = PlayerNameFormatter.Normalize(e.PlayerName);
                var name = playerName.Length > 16
                    ? playerName[..16]
                    : playerName.PadRight(16);
                sb.AppendLine($" {(i + 1),2}.  {name}  {e.Score,9}  {e.TotalKills,5}");
            }

            return sb.ToString().TrimEnd();
        }

        public static bool RefreshCurrentPageIfHighscorePage(ScreenOverlayState overlay, int count = 25)
        {
            if (overlay.Pages.Count == 0 || overlay.CurrentPage < 0 || overlay.CurrentPage >= overlay.Pages.Count)
                return false;

            var page = overlay.Pages[overlay.CurrentPage];
            if (page.Length < 4 || !IsHighscorePageTitle(page[1]))
                return false;

            page[2] = BuildBody(count);
            overlay.ApplyPageContent();
            return true;
        }

        private static bool IsHighscorePageTitle(string title)
        {
            return string.Equals(title, IntroHighscoreTitle, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(title, OutroHighscoreTitle, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
