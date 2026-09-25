using TheOmegaStrain.Domain;
using System;
using System.Linq;
using System.Text;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.Localization;

namespace TheOmegaStrain.Common.Persistence
{
    public static class HighscoreOverlayFormatter
    {
        private const int HeaderLineCount = 2;
        private const int MaxDisplayedHighscoreLines = 20;
        private const int MaxDisplayedHighscoreEntries = MaxDisplayedHighscoreLines - HeaderLineCount;

        public static string BuildBody(int count = MaxDisplayedHighscoreEntries)
        {
            string language = GameState.SettingsState.LanguageCode;
            int displayCount = Math.Clamp(count, 0, MaxDisplayedHighscoreEntries);
            var entries = HighscoreService.GetTopScores(displayCount)
                .OrderByDescending(e => e.Score)
                .Take(displayCount)
                .ToList();

            if (entries.Count == 0)
                return GameText.Get("highscore.empty", language);

            var sb = new StringBuilder();
            sb.AppendLine(GameText.Get("highscore.columns", language));
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

        public static bool RefreshCurrentPageIfHighscorePage(ScreenOverlayState overlay, int count = MaxDisplayedHighscoreEntries)
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
            return GameText.LanguageCodes.Any(language =>
                string.Equals(title, GameText.Get("highscore.introTitle", language), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(title, GameText.Get("highscore.outroTitle", language), StringComparison.OrdinalIgnoreCase));
        }
    }
}
