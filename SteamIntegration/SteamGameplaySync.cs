using Domain;

namespace SteamIntegration;

public sealed class SteamGameplaySync : IDisposable
{
    private readonly SteamManager steamManager;
    private readonly SteamAchievements achievements;
    private readonly SteamLeaderboards leaderboards;
    private readonly SteamStats stats;
    private readonly IGameEventBus? eventBus;
    private readonly Func<SteamGameplaySnapshot> gameplayStateProvider;

    private int lastPowerUpsCollected;
    private int lastSpeedPowerUpLevel;
    private long bestObservedScore;
    private long lastUploadedScore = -1;
    private bool disposed;

    public SteamGameplaySync(
        SteamManager steamManager,
        IGameEventBus? eventBus,
        Func<SteamGameplaySnapshot> gameplayStateProvider)
    {
        this.steamManager = steamManager;
        this.eventBus = eventBus;
        this.gameplayStateProvider = gameplayStateProvider;
        achievements = new SteamAchievements(steamManager);
        leaderboards = new SteamLeaderboards(steamManager);
        stats = new SteamStats(steamManager);

        var gameplay = gameplayStateProvider();
        lastPowerUpsCollected = gameplay.PowerUpsCollected;
        lastSpeedPowerUpLevel = gameplay.SpeedPowerUpLevel;
        bestObservedScore = gameplay.Score;

        Subscribe();
    }

    public bool IsAvailable => steamManager.IsAvailable;

    public void Update()
    {
        if (!steamManager.IsAvailable)
        {
            return;
        }

        steamManager.RunCallbacks();
        SyncProgressionChanges(gameplayStateProvider());
    }

    private void Subscribe()
    {
        eventBus?.Subscribe(GameEventType.EnemyDestroyed, OnEnemyDestroyed);
        eventBus?.Subscribe(GameEventType.PowerUpCollected, OnPowerUpCollected);
        eventBus?.Subscribe(GameEventType.StyleBonusAwarded, OnStyleBonusAwarded);
        eventBus?.Subscribe(GameEventType.SceneCompleted, OnSceneCompleted);
    }

    private void Unsubscribe()
    {
        eventBus?.Unsubscribe(GameEventType.EnemyDestroyed, OnEnemyDestroyed);
        eventBus?.Unsubscribe(GameEventType.PowerUpCollected, OnPowerUpCollected);
        eventBus?.Unsubscribe(GameEventType.StyleBonusAwarded, OnStyleBonusAwarded);
        eventBus?.Unsubscribe(GameEventType.SceneCompleted, OnSceneCompleted);
    }

    private void OnEnemyDestroyed(IGameEvent gameEvent)
    {
        if (!steamManager.IsAvailable || IsTutorial(gameEvent))
        {
            return;
        }

        if (string.Equals(gameEvent.ObjectName, "Seeder", StringComparison.OrdinalIgnoreCase))
        {
            achievements.Unlock(SteamGameConfig.Achievements.FirstSeederDestroyed);
        }
        else if (IsMotherShip(gameEvent.ObjectName))
        {
            achievements.Unlock(SteamGameConfig.Achievements.FirstMothershipDestroyed);
        }

        SyncStats(gameEvent);
        UploadScoreIfImproved(gameEvent.Score);
    }

    private void OnPowerUpCollected(IGameEvent gameEvent)
    {
        if (!steamManager.IsAvailable || IsTutorial(gameEvent))
        {
            return;
        }

        if (gameEvent.PowerUpType != PowerUpType.Standard || gameEvent.SpeedPowerUpLevel > 0)
        {
            achievements.Unlock(SteamGameConfig.Achievements.SpeedUpgradeCollected);
        }

        SyncStats(gameEvent);
        UploadScoreIfImproved(gameEvent.Score);
    }

    private void OnStyleBonusAwarded(IGameEvent gameEvent)
    {
        if (!steamManager.IsAvailable || IsTutorial(gameEvent))
        {
            return;
        }

        if (!gameEvent.HadCollision)
        {
            achievements.Unlock(SteamGameConfig.Achievements.CleanLoop);
        }

        if (gameEvent.AwardedScore > 0)
        {
            stats.SetInt(
                SteamGameConfig.Stats.CleanLoops,
                1,
                storeImmediately: true);
        }
    }

    private void OnSceneCompleted(IGameEvent gameEvent)
    {
        if (!steamManager.IsAvailable || IsTutorial(gameEvent))
        {
            return;
        }

        if (gameEvent.SceneType == SceneTypes.Game || gameEvent.SceneType == SceneTypes.Simulation)
        {
            achievements.Unlock(SteamGameConfig.Achievements.FirstPlanetCleared);

            if (gameEvent.SceneIndex == 6)
            {
                achievements.Unlock(SteamGameConfig.Achievements.DesertPlanetCleared);
            }
        }

        SyncStats(gameEvent);
        UploadScoreIfImproved(gameEvent.Score);
    }

    private void SyncProgressionChanges(SteamGameplaySnapshot gameplay)
    {
        if (gameplay.CurrentSceneType == SceneTypes.Tutorial)
        {
            lastPowerUpsCollected = gameplay.PowerUpsCollected;
            lastSpeedPowerUpLevel = gameplay.SpeedPowerUpLevel;
            return;
        }

        if (gameplay.PowerUpsCollected > lastPowerUpsCollected ||
            gameplay.SpeedPowerUpLevel > lastSpeedPowerUpLevel)
        {
            if (gameplay.SpeedPowerUpLevel > lastSpeedPowerUpLevel)
            {
                achievements.Unlock(SteamGameConfig.Achievements.SpeedUpgradeCollected);
            }

            SyncStats(gameplay);
            UploadScoreIfImproved(gameplay.Score);
        }

        lastPowerUpsCollected = gameplay.PowerUpsCollected;
        lastSpeedPowerUpLevel = gameplay.SpeedPowerUpLevel;
    }

    private void SyncStats(IGameEvent gameEvent)
    {
        bestObservedScore = Math.Max(bestObservedScore, gameEvent.Score);
        stats.SetInt(SteamGameConfig.Stats.BestScore, ToSteamInt(bestObservedScore), storeImmediately: false);
        stats.SetInt(SteamGameConfig.Stats.TotalScore, ToSteamInt(gameEvent.Score), storeImmediately: false);
        stats.SetInt(SteamGameConfig.Stats.TotalKills, gameEvent.TotalKills, storeImmediately: false);
        stats.SetInt(SteamGameConfig.Stats.Deaths, gameEvent.TotalDeaths, storeImmediately: false);
        stats.SetInt(SteamGameConfig.Stats.PlanetsCleared, Math.Max(0, gameEvent.SceneIndex - 1), storeImmediately: false);
        stats.Store();
    }

    private void SyncStats(SteamGameplaySnapshot gameplay)
    {
        bestObservedScore = Math.Max(bestObservedScore, gameplay.Score);
        stats.SetInt(SteamGameConfig.Stats.BestScore, ToSteamInt(bestObservedScore), storeImmediately: false);
        stats.SetInt(SteamGameConfig.Stats.TotalScore, ToSteamInt(gameplay.Score), storeImmediately: false);
        stats.SetInt(SteamGameConfig.Stats.TotalKills, gameplay.TotalKills, storeImmediately: false);
        stats.SetInt(SteamGameConfig.Stats.Deaths, gameplay.TotalDeaths, storeImmediately: false);
        stats.SetInt(SteamGameConfig.Stats.PlanetsCleared, Math.Max(0, gameplay.SceneIndex - 1), storeImmediately: false);
        stats.Store();
    }

    private void UploadScoreIfImproved(long score)
    {
        if (score <= lastUploadedScore)
        {
            return;
        }

        lastUploadedScore = score;
        _ = leaderboards.UploadScoreAsync(SteamGameConfig.Leaderboards.GlobalHighScore, ToSteamInt(score));
    }

    private static bool IsTutorial(IGameEvent gameEvent) => gameEvent.SceneType == SceneTypes.Tutorial;

    private static bool IsMotherShip(string? objectName)
    {
        return string.Equals(objectName, "MotherShipSmall", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(objectName, "MotherShipMedium", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(objectName, "MotherShipLarge", StringComparison.OrdinalIgnoreCase);
    }

    private static int ToSteamInt(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        return value > int.MaxValue ? int.MaxValue : (int)value;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Unsubscribe();
        disposed = true;
    }
}

public readonly record struct SteamGameplaySnapshot(
    SceneTypes CurrentSceneType,
    int SceneIndex,
    long Score,
    int TotalKills,
    int TotalShotsFired,
    int TotalDeaths,
    float Accuracy,
    int PowerUpsCollected,
    int SpeedPowerUpLevel);
