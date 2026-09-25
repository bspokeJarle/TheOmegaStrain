using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TheOmegaStrain.Common.Localization
{
    /// <summary>Small, read-only text catalog. Missing translations fall back to English.</summary>
    public static class GameText
    {
        public static readonly string[] LanguageCodes = { "en", "no", "de", "fr", "pl" };

        private static readonly Dictionary<string, Dictionary<string, string>> Catalogs = LoadCatalogs();

        public static string Get(string key, string? languageCode)
        {
            string code = NormalizeLanguageCode(languageCode);
            if (Catalogs.TryGetValue(code, out var translated) &&
                translated.TryGetValue(key, out string? value) &&
                !string.IsNullOrWhiteSpace(value))
                return value;

            return Catalogs.TryGetValue("en", out var english) &&
                   english.TryGetValue(key, out string? fallback)
                ? fallback : key;
        }

        // The catalog owns the sentence; callers provide only the changing values.
        public static string Format(string key, string? languageCode, params (string Name, string Value)[] values)
        {
            string text = Get(key, languageCode);
            foreach (var (name, value) in values)
                text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
            return text;
        }

        public static string NormalizeLanguageCode(string? code)
        {
            foreach (string supported in LanguageCodes)
            {
                if (string.Equals(code, supported, StringComparison.OrdinalIgnoreCase))
                    return supported;
            }

            return "en";
        }

        private static Dictionary<string, Dictionary<string, string>> LoadCatalogs()
        {
            var catalogs = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (string code in LanguageCodes)
            {
                string resourceName = $"TheOmegaStrain.Common.Localization.language-{code}.json";
                using Stream? stream = typeof(GameText).Assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    continue;

                try
                {
                    catalogs[code] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                        ?? new Dictionary<string, string>();
                }
                catch (JsonException)
                {
                    // A damaged optional translation must never prevent the game from starting.
                }
            }

            return catalogs;
        }
    }
}
