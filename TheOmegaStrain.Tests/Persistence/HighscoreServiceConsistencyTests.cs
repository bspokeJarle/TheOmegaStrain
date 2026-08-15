using TheOmegaStrain.Common.Persistence;
using System;
using System.IO;
using System.Linq;

namespace TheOmegaStrain.Tests.Persistence;

[TestClass]
public class HighscoreServiceConsistencyTests
{
    private string _originalLocalFolder = string.Empty;
    private string _testLocalFolder = string.Empty;
    private int _originalMaxEntries;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _originalMaxEntries = PersistenceSetup.MaxHighscoreEntries;

        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainHighscoreTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.MaxHighscoreEntries = 100;
        PersistenceSetup.Initialize();
    }

    [TestCleanup]
    public void Cleanup()
    {
        PersistenceSetup.LocalFolder = _originalLocalFolder;
        PersistenceSetup.MaxHighscoreEntries = _originalMaxEntries;

        try
        {
            if (Directory.Exists(_testLocalFolder))
                Directory.Delete(_testLocalFolder, recursive: true);
        }
        catch
        {
        }
    }

    [TestMethod]
    public void TrySubmitScore_UpdatesExistingPlayerInsteadOfAddingDuplicate()
    {
        bool first = HighscoreService.TrySubmitScore("Jarle", 1000, 1);
        bool second = HighscoreService.TrySubmitScore("Jarle", 1500, 2);

        var list = HighscoreService.LoadLocalHighscores();
        var jarleEntries = list.Entries.FindAll(e => string.Equals(e.PlayerName, "Jarle", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(first);
        Assert.IsTrue(second);
        Assert.AreEqual(1, jarleEntries.Count, "Same player should have exactly one highscore row.");
        Assert.AreEqual(1500L, jarleEntries[0].Score, "Existing row should be updated to higher score.");
    }

    [TestMethod]
    public void TrySubmitScore_TrimsPlayerNameAndStillUpdatesSameRecord()
    {
        bool first = HighscoreService.TrySubmitScore("Jarle", 1200, 1);
        bool second = HighscoreService.TrySubmitScore("  Jarle  ", 1300, 2);

        var list = HighscoreService.LoadLocalHighscores();
        var jarleEntries = list.Entries.FindAll(e => string.Equals(e.PlayerName, "Jarle", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(first);
        Assert.IsTrue(second);
        Assert.AreEqual(1, jarleEntries.Count,
            "Whitespace variants of the same player name should not create duplicate rows.");
        Assert.AreEqual(1300L, jarleEntries[0].Score);
    }

    [TestMethod]
    public void TrySubmitScore_NormalizesPlayerNameToUppercase()
    {
        HighscoreService.TrySubmitScore("  CharlieB  ", 1200, 1);

        var entry = HighscoreService.LoadLocalHighscores().Entries.Single();

        Assert.AreEqual("CHARLIEB", entry.PlayerName);
    }

    [TestMethod]
    public void GetBestLocalScore_ReturnsPersistedScoreForPlayerCaseInsensitively()
    {
        HighscoreService.TrySubmitScore("CharlieB", 50150, 5);

        Assert.AreEqual(50150L, HighscoreService.GetBestLocalScore(" CHARLIEB "));
        Assert.AreEqual(0L, HighscoreService.GetBestLocalScore("Unknown"));
    }

    [TestMethod]
    public void SaveLocalHighscores_NeverReplacesHigherScoreWithLowerScore()
    {
        HighscoreService.TrySubmitScore("CharlieB", 50150, 5);

        HighscoreService.SaveLocalHighscores(new HighscoreList
        {
            Entries = new List<HighscoreEntry>
            {
                new() { PlayerName = "CharlieB", Score = 0, WaveReached = 1 }
            }
        });

        Assert.AreEqual(50150L, HighscoreService.GetBestLocalScore("CharlieB"));
    }

    [TestMethod]
    public void TrySubmitScore_DoesNotResetExistingKillsWhenLegacyCallOmitsStats()
    {
        HighscoreService.TrySubmitScore(
            "CharlieB",
            50150,
            5,
            totalKills: 104,
            totalShotsFired: 391,
            totalDeaths: 47,
            accuracy: 104f / 391f);

        HighscoreService.TrySubmitScore("CharlieB", 52000, 6);

        var entry = HighscoreService.LoadLocalHighscores().Entries.Single(e => e.PlayerName == "CHARLIEB");

        Assert.AreEqual(52000L, entry.Score);
        Assert.AreEqual(104, entry.TotalKills,
            "A higher score submitted through the legacy overload must not wipe stored kill stats.");
        Assert.AreEqual(391, entry.TotalShotsFired);
        Assert.AreEqual(47, entry.TotalDeaths);
        Assert.IsTrue(entry.Accuracy > 0f);
    }

    [TestMethod]
    public void SaveLocalHighscores_DoesNotResetKillsWhenIncomingDuplicateHasDefaultStats()
    {
        HighscoreService.TrySubmitScore(
            "CharlieB",
            50150,
            5,
            totalKills: 104,
            totalShotsFired: 391,
            totalDeaths: 47,
            accuracy: 104f / 391f);

        HighscoreService.SaveLocalHighscores(new HighscoreList
        {
            Entries = new List<HighscoreEntry>
            {
                new()
                {
                    PlayerName = "CharlieB",
                    Score = 50150,
                    WaveReached = 5,
                    TotalKills = 0,
                    TotalShotsFired = 0,
                    TotalDeaths = 0,
                    Accuracy = 0f,
                    DateUtc = DateTime.UtcNow.AddMinutes(1).ToString("o")
                }
            }
        });

        var entry = HighscoreService.LoadLocalHighscores().Entries.Single(e => e.PlayerName == "CHARLIEB");

        Assert.AreEqual(50150L, entry.Score);
        Assert.AreEqual(104, entry.TotalKills,
            "Remote/local merges must not prefer a newer duplicate when it only contributes default stat values.");
        Assert.AreEqual(391, entry.TotalShotsFired);
        Assert.AreEqual(47, entry.TotalDeaths);
        Assert.IsTrue(entry.Accuracy > 0f);
    }

    [TestMethod]
    public void TrySubmitScore_RepairsExistingZeroKillsWhenSameScoreProvidesStats()
    {
        HighscoreService.SaveLocalHighscores(new HighscoreList
        {
            Entries = new List<HighscoreEntry>
            {
                new()
                {
                    PlayerName = "CharlieB",
                    Score = 50150,
                    WaveReached = 5,
                    TotalKills = 0,
                    TotalShotsFired = 0,
                    TotalDeaths = 0,
                    Accuracy = 0f,
                    DateUtc = DateTime.UtcNow.ToString("o")
                }
            }
        });

        bool repaired = HighscoreService.TrySubmitScore(
            "CharlieB",
            50150,
            5,
            totalKills: 104,
            totalShotsFired: 391,
            totalDeaths: 47,
            accuracy: 104f / 391f);

        var entry = HighscoreService.LoadLocalHighscores().Entries.Single(e => e.PlayerName == "CHARLIEB");

        Assert.IsTrue(repaired,
            "A same-score stat repair should be treated as a successful submit so remote sync can heal too.");
        Assert.AreEqual(104, entry.TotalKills);
        Assert.AreEqual(391, entry.TotalShotsFired);
        Assert.AreEqual(47, entry.TotalDeaths);
        Assert.IsTrue(entry.Accuracy > 0f);
    }

    [TestMethod]
    public void LoadLocalHighscores_UsesEncryptedBackupWhenPrimaryIsMissing()
    {
        HighscoreService.TrySubmitScore("CharlieB", 50150, 5);
        File.Delete(PersistenceSetup.LocalHighscoreFilePath);

        Assert.AreEqual(50150L, HighscoreService.GetBestLocalScore("CharlieB"));
    }
}
