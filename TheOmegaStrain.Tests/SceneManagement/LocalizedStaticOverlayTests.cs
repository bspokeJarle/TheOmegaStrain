using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Localization;
using TheOmegaStrain.Game.Scenes.Intro;
using TheOmegaStrain.Game.Scenes.Outro;
using TheOmegaStrain.Game.Scenes.Tutorial;
using TheOmegaStrain.Game.Scenes.Scene1;
using TheOmegaStrain.Game.Scenes.Scene6;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class LocalizedStaticOverlayTests
{
    private GameSettingsState _previousSettings = null!;

    [TestInitialize]
    public void SaveSettings() => _previousSettings = GameState.SettingsState;

    [TestCleanup]
    public void RestoreSettings() => GameState.SettingsState = _previousSettings;

    private static readonly string[] PageKeys =
    [
        "intro.infoFooter", "intro.story.header", "intro.story.title", "intro.story.body",
        "intro.manual.header", "intro.tips.title", "intro.tips.body",
        "intro.weapons.title", "intro.weapons.body", "intro.shields.title", "intro.shields.body",
        "intro.flight.title", "intro.flight.body", "intro.hud.title", "intro.hud.body",
        "training.intro.header", "training.intro.title", "training.intro.body", "training.intro.footer",
        "outro.victory.header", "outro.victory.title", "outro.victory.body", "outro.victory.footer",
        "outro.simulation.header", "outro.simulation.title", "outro.simulation.body", "outro.simulation.footer",
        "menu.activeControlLine", "menu.languageChoice"
    ];

    [TestMethod]
    public void StaticPages_HaveTranslationsInEverySupportedLanguage()
    {
        string[] englishKeys = ReadCatalog("en").Keys.OrderBy(key => key).ToArray();
        foreach (string language in GameText.LanguageCodes)
        {
            CollectionAssert.AreEqual(englishKeys, ReadCatalog(language).Keys.OrderBy(key => key).ToArray(),
                $"Catalog {language} must have the same keys as English.");
        foreach (string key in PageKeys)
            Assert.AreNotEqual(key, GameText.Get(key, language), $"Missing {language}: {key}");
        }
    }

    private static Dictionary<string, string> ReadCatalog(string language)
    {
        using Stream stream = typeof(GameText).Assembly.GetManifestResourceStream(
            $"TheOmegaStrain.Common.Localization.language-{language}.json")!;
        Assert.IsNotNull(stream);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }

    [TestMethod]
    public void Catalogs_LoadWithoutDuplicateKeysOrEmptyValues()
    {
        foreach (string language in GameText.LanguageCodes)
        {
            using Stream stream = typeof(GameText).Assembly.GetManifestResourceStream(
                $"TheOmegaStrain.Common.Localization.language-{language}.json")!;
            Assert.IsNotNull(stream, $"Missing catalog: {language}");
            using JsonDocument document = JsonDocument.Parse(stream);
            Assert.AreEqual(JsonValueKind.Object, document.RootElement.ValueKind, language);

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty entry in document.RootElement.EnumerateObject())
            {
                Assert.IsTrue(names.Add(entry.Name), $"Duplicate key: {language}:{entry.Name}");
                Assert.AreEqual(JsonValueKind.String, entry.Value.ValueKind, $"Non-text value: {language}:{entry.Name}");
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Value.GetString()),
                    $"Empty value: {language}:{entry.Name}");
            }
        }
    }

    [TestMethod]
    public void InvalidLanguageAndUnknownKey_HaveVisibleEnglishFallback()
    {
        const string key = "menu.start";
        Assert.AreEqual(GameText.Get(key, "en"), GameText.Get(key, "unsupported"));
        Assert.AreEqual(GameText.Get(key, "en"), GameText.Get(key, null));
        Assert.AreEqual("missing.key", GameText.Get("missing.key", "pl"));
    }

    [TestMethod]
    public void NamedTokens_UseValuesWithoutTranslatingTheirNames()
    {
        foreach (string language in GameText.LanguageCodes)
        {
            string line = GameText.Format("menu.activeControlLine", language, ("control", "TEST CONTROL"));
            StringAssert.Contains(line, "TEST CONTROL");
            Assert.IsFalse(line.Contains("{control}"));
        }
    }

    [TestMethod]
    public void SelectedLanguage_IsUsedByIntroTrainingAndOutroPages()
    {
        GameState.SettingsState = new GameSettingsState { LanguageCode = "no" };
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.WorldFade = new WorldFadeState();

        new Intro().SetupSceneOverlay();
        Assert.AreEqual(GameText.Get("intro.story.title", "no"), GameState.ScreenOverlayState.Pages[1][1]);

        new TutorialScene().SetupSceneOverlay();
        Assert.AreEqual(GameText.Get("training.intro.body", "no"), GameState.ScreenOverlayState.Body);

        var method = typeof(OutroDirector).GetMethod("ShowCongratulationsOverlay", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method);
        method.Invoke(null, [GameState.ScreenOverlayState]);
        Assert.AreEqual(GameText.Get("outro.victory.title", "no"), GameState.ScreenOverlayState.Pages[0][1]);
        Assert.AreEqual(GameText.Get("highscore.outroTitle", "no"), GameState.ScreenOverlayState.Pages[2][1]);
    }

    [TestMethod]
    public void TranslatedTemplates_HaveTheSameNamedTokensAsEnglish()
    {
        var english = ReadCatalog("en");
        foreach (string language in GameText.LanguageCodes)
        {
            var catalog = ReadCatalog(language);
            foreach (var (key, text) in english)
            {
                string[] expected = Regex.Matches(text, @"\{[a-zA-Z][a-zA-Z0-9]*\}")
                    .Select(match => match.Value).Order().ToArray();
                string[] actual = Regex.Matches(catalog[key], @"\{[a-zA-Z][a-zA-Z0-9]*\}")
                    .Select(match => match.Value).Order().ToArray();
                CollectionAssert.AreEqual(expected, actual, $"Token mismatch: {language}:{key}");
            }
        }
    }

    [TestMethod]
    public void CampaignBriefings_ResolveDynamicTokensInEveryLanguage()
    {
        foreach (string language in GameText.LanguageCodes)
        {
            GameState.SettingsState = new GameSettingsState { LanguageCode = language };
            GameState.ScreenOverlayState = new ScreenOverlayState();
            new Scene1().SetupSceneOverlay();
            StringAssert.Contains(GameState.ScreenOverlayState.Body, "7");
            StringAssert.Contains(GameState.ScreenOverlayState.Body, "14.0");
            Assert.IsFalse(GameState.ScreenOverlayState.Body.Contains('{'));

            new Scene6().SetupSceneOverlay();
            StringAssert.Contains(GameState.ScreenOverlayState.Body, "21");
            StringAssert.Contains(GameState.ScreenOverlayState.Body, "14");
            StringAssert.Contains(GameState.ScreenOverlayState.Body, "1.8");
            Assert.IsFalse(GameState.ScreenOverlayState.Body.Contains('{'));
        }
    }

    [TestMethod]
    public void SettingsAndCallsignMenu_UseSelectedLanguageWithoutChangingChoiceCount()
    {
        foreach (string language in GameText.LanguageCodes)
        {
            GameState.SettingsState = new GameSettingsState { LanguageCode = language };
            GameState.ScreenOverlayState = new ScreenOverlayState();
            var settings = GameState.SettingsState;

            StringAssert.Contains(GameSettingsOverlayFormatter.BuildAudioBody(settings, 0),
                GameText.Get("settings.master", language));
            StringAssert.Contains(GameSettingsOverlayFormatter.BuildGraphicsBody(settings, 0),
                GameText.Get("settings.quality", language));
            StringAssert.Contains(GameSettingsOverlayFormatter.BuildControlsBody(settings, 0),
                GameText.Get("settings.playUsing", language));
            StringAssert.Contains(GameSettingsOverlayFormatter.BuildFlightBody(settings, 0),
                GameText.Get("settings.flightFeel", language));

            GameState.ScreenOverlayState.SetNameEntryPreset("TEST");
            Assert.AreEqual(4, GameState.ScreenOverlayState.ChoiceOptions.Count);
            Assert.AreEqual(GameText.Get("callsign.use", language), GameState.ScreenOverlayState.ChoiceOptions[0]);
        }
    }
}
