using _3dRotations.World;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.Persistence;
using Domain;

namespace _3DSpesificsUnitTests.Persistence;

[TestClass]
public class HighscoreRemoteFallbackTests
{
    private string _originalLocalFolder = "";
    private string _testLocalFolder = "";
    private int _originalMaxEntries;
    private string? _originalSupabaseUrl;
    private string? _originalSupabaseAnonKey;
    private string _originalSupabaseTableName = "";
    private TimeSpan _originalRemoteFetchCooldown;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _originalMaxEntries = PersistenceSetup.MaxHighscoreEntries;
        _originalSupabaseUrl = PersistenceSetup.SupabaseUrl;
        _originalSupabaseAnonKey = PersistenceSetup.SupabaseAnonKey;
        _originalSupabaseTableName = PersistenceSetup.SupabaseTableName;
        _originalRemoteFetchCooldown = PersistenceSetup.RemoteFetchCooldown;

        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainRemoteHighscoreTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.MaxHighscoreEntries = 100;
        PersistenceSetup.SupabaseUrl = null;
        PersistenceSetup.SupabaseAnonKey = null;
        PersistenceSetup.SupabaseTableName = "highscores";
        PersistenceSetup.RemoteFetchCooldown = TimeSpan.FromMinutes(10);
        HighscoreService.ResetRemoteFetchStateForTests();
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.WorldFade = new WorldFadeState();
        GameState.ObjectIdCounter = 0;
        GameState.DeltaTime = 0f;
        PersistenceSetup.Initialize();
        HighscoreService.SaveLocalHighscores(new HighscoreList());
    }

    [TestCleanup]
    public void Cleanup()
    {
        HighscoreService.ResetRemoteFetchStateForTests();
        PersistenceSetup.LocalFolder = _originalLocalFolder;
        PersistenceSetup.MaxHighscoreEntries = _originalMaxEntries;
        PersistenceSetup.SupabaseUrl = _originalSupabaseUrl;
        PersistenceSetup.SupabaseAnonKey = _originalSupabaseAnonKey;
        PersistenceSetup.SupabaseTableName = _originalSupabaseTableName;
        PersistenceSetup.RemoteFetchCooldown = _originalRemoteFetchCooldown;

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
    public void BuildBody_UsesRemoteScoresWhenSupabaseIsConfigured()
    {
        ConfigureSupabase();
        HighscoreService.FetchRemoteTopScoresAsync = _ =>
            Task.FromResult<List<HighscoreEntry>?>(new List<HighscoreEntry>
            {
                CreateEntry("REMOTEACE", 54950, 104)
            });

        string body = HighscoreOverlayFormatter.BuildBody();

        Assert.IsTrue(body.Contains("REMOTEACE"));
        Assert.IsFalse(body.Contains("No highscores recorded yet"));

        var cached = HighscoreService.LoadLocalHighscores();
        Assert.AreEqual(1, cached.Entries.Count);
        Assert.AreEqual("REMOTEACE", cached.Entries[0].PlayerName);
    }

    [TestMethod]
    public void BuildBody_UsesLocalScoresWhenRemoteFetchFails()
    {
        ConfigureSupabase();
        HighscoreService.SaveLocalHighscores(new HighscoreList
        {
            Entries = new List<HighscoreEntry>
            {
                CreateEntry("LOCALACE", 12345, 12)
            }
        });
        HighscoreService.FetchRemoteTopScoresAsync = _ =>
            Task.FromException<List<HighscoreEntry>?>(new HttpRequestException("offline"));

        string body = HighscoreOverlayFormatter.BuildBody();

        Assert.IsTrue(body.Contains("LOCALACE"));
        Assert.IsFalse(body.Contains("No highscores recorded yet"));
    }

    [TestMethod]
    public void GetTopScores_RetriesRemoteFetchAfterNullResponse()
    {
        ConfigureSupabase();
        int callCount = 0;
        HighscoreService.FetchRemoteTopScoresAsync = _ =>
        {
            callCount++;
            if (callCount == 1)
                return Task.FromResult<List<HighscoreEntry>?>(null);

            return Task.FromResult<List<HighscoreEntry>?>(new List<HighscoreEntry>
            {
                CreateEntry("RETRYACE", 22222, 22)
            });
        };

        var first = HighscoreService.GetTopScores(25);
        var second = HighscoreService.GetTopScores(25);

        Assert.AreEqual(0, first.Count);
        Assert.AreEqual(2, callCount);
        Assert.AreEqual(1, second.Count);
        Assert.AreEqual("RETRYACE", second[0].PlayerName);
    }

    [TestMethod]
    public void SupabaseHighscoreClient_ParsesSnakeCaseRows()
    {
        const string body = """
        [
          {
            "player_name": "charliea",
            "score": 54950,
            "wave_reached": 4,
            "total_kills": 104,
            "total_shots_fired": 391,
            "total_deaths": 47,
            "accuracy": 0.265985,
            "date_utc": "2026-04-19T19:59:23.1512993Z"
          }
        ]
        """;

        var entries = SupabaseHighscoreClient.ParseHighscoreRows(body);

        Assert.IsNotNull(entries);
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("CHARLIEA", entries[0].PlayerName);
        Assert.AreEqual(54950, entries[0].Score);
        Assert.AreEqual(104, entries[0].TotalKills);
    }

    [TestMethod]
    public void RefreshCurrentPageIfHighscorePage_RebuildsStaleIntroHighscoreBody()
    {
        ConfigureSupabase();
        HighscoreService.FetchRemoteTopScoresAsync = _ =>
            Task.FromResult<List<HighscoreEntry>?>(new List<HighscoreEntry>
            {
                CreateEntry("REFRESHACE", 44444, 44)
            });
        var overlay = new ScreenOverlayState();
        overlay.AddPage("RETROMESH SYSTEM INITIALIZING", "THE OMEGA STRAIN", "story", "footer");
        overlay.AddPage("RETROMESH // FIELD MANUAL", "TACTICAL BRIEFING", "tips", "footer");
        overlay.AddPage(
            "RETROMESH // HALL OF FAME",
            "TOP PILOTS",
            "No highscores recorded yet.\n\nBe the first pilot to make history!",
            "footer");
        overlay.CurrentPage = 2;
        overlay.ApplyPageContent();

        bool refreshed = HighscoreOverlayFormatter.RefreshCurrentPageIfHighscorePage(overlay);

        Assert.IsTrue(refreshed);
        Assert.IsTrue(overlay.Body.Contains("REFRESHACE"));
        Assert.IsTrue(overlay.Pages[2][2].Contains("REFRESHACE"));
        Assert.IsFalse(overlay.Body.Contains("No highscores recorded yet"));
    }

    [TestMethod]
    public void WorldStartup_InitializesPersistenceBeforeIntroOverlayBuildsHighscores()
    {
        File.WriteAllText(
            Path.Combine(_testLocalFolder, "secrets.json"),
            """
            {
              "SupabaseUrl": "https://example.supabase.co",
              "SupabaseAnonKey": "test-anon-key"
            }
            """);
        PersistenceSetup.SupabaseUrl = null;
        PersistenceSetup.SupabaseAnonKey = null;
        HighscoreService.ResetRemoteFetchStateForTests();
        HighscoreService.FetchRemoteTopScoresAsync = _ =>
            Task.FromResult<List<HighscoreEntry>?>(new List<HighscoreEntry>
            {
                CreateEntry("STARTUPACE", 33333, 33)
            });

        _ = new GameWorld();

        Assert.IsTrue(PersistenceSetup.IsSupabaseConfigured);
        var highscorePage = GameState.ScreenOverlayState.Pages.Single(page => page[1] == "TOP PILOTS");
        Assert.IsTrue(highscorePage[2].Contains("STARTUPACE"));
        Assert.IsFalse(highscorePage[2].Contains("No highscores recorded yet"));
    }

    private static void ConfigureSupabase()
    {
        PersistenceSetup.SupabaseUrl = "https://example.supabase.co";
        PersistenceSetup.SupabaseAnonKey = "test-anon-key";
    }

    private static HighscoreEntry CreateEntry(string name, long score, int kills)
    {
        return new HighscoreEntry
        {
            PlayerName = name,
            Score = score,
            WaveReached = 1,
            TotalKills = kills,
            DateUtc = new DateTime(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc).ToString("o")
        };
    }
}
