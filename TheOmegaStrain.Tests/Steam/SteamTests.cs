using TheOmegaStrain.Steam;
using TheOmegaStrain.Common.Events;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.Steam;

[TestClass]
public sealed class SteamTests
{
    [TestMethod]
    public void SteamConfigUsesTheOmegaStrainAppId()
    {
        Assert.AreEqual<uint>(480, SteamGameConfig.SpaceWarSampleAppId);
        Assert.AreEqual<uint>(4952000, SteamGameConfig.ProductionAppId);
        Assert.AreEqual(SteamGameConfig.ProductionAppId, SteamGameConfig.DevelopmentAppId);
#if DEBUG
        Assert.AreEqual(SteamGameConfig.DevelopmentAppId, SteamGameConfig.RuntimeAppId);
#else
        Assert.AreEqual(SteamGameConfig.ProductionAppId, SteamGameConfig.RuntimeAppId);
#endif
    }

    [TestMethod]
    public void SteamApiNamesAreCentralizedAndNonEmpty()
    {
        AssertSteamName(SteamGameConfig.Achievements.TrainingComplete);
        AssertSteamName(SteamGameConfig.Achievements.FirstSeederDestroyed);
        AssertSteamName(SteamGameConfig.Achievements.FirstPlanetCleared);
        AssertSteamName(SteamGameConfig.Achievements.FirstMothershipDestroyed);
        AssertSteamName(SteamGameConfig.Achievements.CleanLoop);
        AssertSteamName(SteamGameConfig.Achievements.LowAltitudeRun);
        AssertSteamName(SteamGameConfig.Achievements.DecoyKill);
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
            SpeedPowerUpLevel: 0,
            TutorialCompleted: false);

        using var sync = new SteamGameplaySync(manager, bus, () => gameplay);

        Assert.IsFalse(sync.CurrentStatsRequested);

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
    public void SteamGameplaySync_DetectsDecoyKillFromEnemyImpactSource()
    {
        var seeder = new OmegaObject3D
        {
            ObjectId = 1,
            ObjectName = "Seeder",
            CrashBoxes = new List<List<IVector3>>(),
            ImpactStatus = new ImpactStatus { ObjectName = "DroneDecoy" }
        };

        var gameEvent = new GameEvent
        {
            Type = GameEventType.EnemyDestroyed,
            Source = seeder,
            ObjectName = "Seeder",
            SceneType = SceneTypes.Game
        };

        Assert.IsTrue(SteamGameplaySync.IsDecoyKill(gameEvent));
    }

    [TestMethod]
    public void SteamGameplaySync_DoesNotTreatNormalWeaponKillAsDecoyKill()
    {
        var seeder = new OmegaObject3D
        {
            ObjectId = 1,
            ObjectName = "Seeder",
            CrashBoxes = new List<List<IVector3>>(),
            ImpactStatus = new ImpactStatus { ObjectName = "Lazer" }
        };

        var gameEvent = new GameEvent
        {
            Type = GameEventType.EnemyDestroyed,
            Source = seeder,
            ObjectName = "Seeder",
            SceneType = SceneTypes.Game
        };

        Assert.IsFalse(SteamGameplaySync.IsDecoyKill(gameEvent));
    }

    [TestMethod]
    public void SteamGameplaySync_SeparatesLowAltitudeStyleBonusFromCleanLoop()
    {
        var lowAltitudeEvent = new GameEvent
        {
            Type = GameEventType.StyleBonusAwarded,
            StyleBonusType = StyleBonusTypes.LowAltitudeRun,
            AwardedScore = 250,
            HadCollision = false,
            SceneType = SceneTypes.Game
        };

        Assert.IsTrue(SteamGameplaySync.IsLowAltitudeStyleBonus(lowAltitudeEvent));
        Assert.IsFalse(SteamGameplaySync.IsCleanLoopStyleBonus(lowAltitudeEvent));
    }

    [TestMethod]
    public void SteamGameplaySync_KeepsLegacyCleanLoopFallbackForUntypedStyleBonus()
    {
        var legacyCleanLoopEvent = new GameEvent
        {
            Type = GameEventType.StyleBonusAwarded,
            AwardedScore = 250,
            HadCollision = false,
            SceneType = SceneTypes.Game
        };

        Assert.IsTrue(SteamGameplaySync.IsCleanLoopStyleBonus(legacyCleanLoopEvent));
    }

    [TestMethod]
    public void SteamGameplaySync_DetectsTrainingCompletedScene()
    {
        var tutorialCompletedEvent = new GameEvent
        {
            Type = GameEventType.SceneCompleted,
            ObjectName = SceneTypes.Tutorial.ToString(),
            SceneType = SceneTypes.Tutorial,
            SceneIndex = 11
        };

        Assert.IsTrue(SteamGameplaySync.IsTrainingCompletedScene(tutorialCompletedEvent));
    }

    [TestMethod]
    public void SteamAppIdFileIsCopiedForDebugRuns()
    {
        var appIdPath = Path.Combine(AppContext.BaseDirectory, "steam_appid.txt");

        Assert.IsTrue(File.Exists(appIdPath));
        Assert.AreEqual(SteamGameConfig.DevelopmentAppId.ToString(), File.ReadAllText(appIdPath).Trim());
    }

    [TestMethod]
    public void SteamLiveSmokeCanInitializeWhenSteamClientIsAvailable()
    {
        using var manager = new SteamManager();

        if (!manager.Initialize(SteamGameConfig.RuntimeAppId))
        {
            Assert.Inconclusive($"Steam unavailable for live smoke test: {manager.LastError ?? "SteamAPI.Init returned false"}");
        }

        manager.RunCallbacks();

        Assert.AreEqual(SteamGameConfig.RuntimeAppId, manager.AppId);
        Assert.AreNotEqual<ulong>(0, manager.SteamId);

        var stats = new SteamStats(manager);
        Assert.IsTrue(stats.RequestCurrentStats());
    }

    private static void AssertSteamName(string name)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(name));
        StringAssert.Matches(name, new System.Text.RegularExpressions.Regex("^[A-Z0-9_]+$"));
    }
}
