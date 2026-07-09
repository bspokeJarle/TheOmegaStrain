using SteamIntegration;
using CommonUtilities.Events;
using Domain;

namespace _3DSpesificsUnitTests.SteamIntegration;

[TestClass]
public sealed class SteamIntegrationTests
{
    [TestMethod]
    public void SteamConfigUsesSpaceWarForDevelopmentOnly()
    {
        Assert.AreEqual<uint>(480, SteamGameConfig.DevelopmentAppId);
        Assert.AreEqual<uint>(0, SteamGameConfig.ProductionAppId);
    }

    [TestMethod]
    public void SteamApiNamesAreCentralizedAndNonEmpty()
    {
        AssertSteamName(SteamGameConfig.Achievements.TrainingComplete);
        AssertSteamName(SteamGameConfig.Achievements.FirstSeederDestroyed);
        AssertSteamName(SteamGameConfig.Achievements.FirstPlanetCleared);
        AssertSteamName(SteamGameConfig.Achievements.FirstMothershipDestroyed);
        AssertSteamName(SteamGameConfig.Leaderboards.GlobalHighScore);
        AssertSteamName(SteamGameConfig.Stats.BestScore);
        AssertSteamName(SteamGameConfig.Stats.TotalKills);
    }

    [TestMethod]
    public void SteamWrappersNoOpWhenSteamIsNotInitialized()
    {
        using var manager = new SteamManager();
        var achievements = new SteamAchievements(manager);
        var stats = new SteamStats(manager);

        manager.RunCallbacks();
        manager.Shutdown();

        Assert.IsFalse(manager.IsAvailable);
        Assert.IsFalse(achievements.Unlock(SteamGameConfig.Achievements.TrainingComplete));
        Assert.IsFalse(achievements.Clear(SteamGameConfig.Achievements.TrainingComplete));
        Assert.IsFalse(achievements.TryGetUnlocked(SteamGameConfig.Achievements.TrainingComplete, out var unlocked));
        Assert.IsFalse(unlocked);
        Assert.IsFalse(stats.RequestCurrentStats());
        Assert.IsFalse(stats.SetInt(SteamGameConfig.Stats.BestScore, 1000));
        Assert.IsFalse(stats.TryGetInt(SteamGameConfig.Stats.BestScore, out var score));
        Assert.AreEqual(0, score);
    }

    [TestMethod]
    public async Task SteamLeaderboardsNoOpWhenSteamIsNotInitialized()
    {
        using var manager = new SteamManager();
        var leaderboards = new SteamLeaderboards(manager);

        var leaderboard = await leaderboards.FindAsync(SteamGameConfig.Leaderboards.GlobalHighScore);
        var uploaded = await leaderboards.UploadScoreAsync(SteamGameConfig.Leaderboards.GlobalHighScore, 1000);

        Assert.IsNull(leaderboard);
        Assert.IsFalse(uploaded);
    }

    [TestMethod]
    public void SteamGameplaySyncNoOpsWhenSteamIsNotInitialized()
    {
        using var manager = new SteamManager();
        var bus = new GameEventBus();
        var gameplay = new SteamGameplaySnapshot(
            SceneTypes.Game,
            SceneIndex: 1,
            Score: 1000,
            TotalKills: 1,
            TotalShotsFired: 2,
            TotalDeaths: 0,
            Accuracy: 0.5f,
            PowerUpsCollected: 0,
            SpeedPowerUpLevel: 0);

        using var sync = new SteamGameplaySync(manager, bus, () => gameplay);

        sync.Update();
        bus.Publish(new GameEvent
        {
            Type = GameEventType.EnemyDestroyed,
            ObjectName = "Seeder",
            SceneType = SceneTypes.Game,
            SceneIndex = 1,
            Score = gameplay.Score,
            TotalKills = gameplay.TotalKills
        });

        Assert.IsFalse(sync.IsAvailable);
    }

    [TestMethod]
    public void SteamAppIdFileIsCopiedForDebugRuns()
    {
        var appIdPath = Path.Combine(AppContext.BaseDirectory, "steam_appid.txt");

        Assert.IsTrue(File.Exists(appIdPath));
        Assert.AreEqual("480", File.ReadAllText(appIdPath).Trim());
    }

    [TestMethod]
    public void SteamLiveSmokeCanInitializeWhenSteamClientIsAvailable()
    {
        using var manager = new SteamManager();

        if (!manager.Initialize())
        {
            Assert.Inconclusive($"Steam unavailable for live smoke test: {manager.LastError ?? "SteamAPI.Init returned false"}");
        }

        manager.RunCallbacks();

        Assert.AreEqual(SteamGameConfig.DevelopmentAppId, manager.AppId);
        Assert.AreNotEqual<ulong>(0, manager.SteamId);
    }

    private static void AssertSteamName(string name)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(name));
        StringAssert.Matches(name, new System.Text.RegularExpressions.Regex("^[A-Z0-9_]+$"));
    }
}
