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
    private readonly HashSet<string> unlockedAchievements = new(StringComparer.OrdinalIgnoreCase);
    private readonly object leaderboardUploadGate = new();

    private int lastPowerUpsCollected;
    private int lastSpeedPowerUpLevel;
    private long bestObservedScore;
    private long lastConfirmedLeaderboardScore = -1;
    private long inFlightLeaderboardScore = -1;
    private long queuedLeaderboardScore = -1;
    private bool leaderboardUploadInFlight;
    private SteamSyncedStats? lastSyncedStats;
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
        CurrentStatsRequested = stats.RequestCurrentStats();
        SteamDiagnostics.Write(
            $"[Sync] created available={steamManager.IsAvailable} currentStatsRequested={CurrentStatsRequested} " +
            $"sceneType={gameplay.CurrentSceneType} sceneIndex={gameplay.SceneIndex} score={gameplay.Score} kills={gameplay.TotalKills}");

        Subscribe();
    }

    public bool IsAvailable => steamManager.IsAvailable;

    public bool CurrentStatsRequested { get; }

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
            SteamDiagnostics.Write($"[Sync] enemyDestroyed skipped available={steamManager.IsAvailable} tutorial={IsTutorial(gameEvent)} object='{gameEvent.ObjectName}'");
            return;
        }

        SteamDiagnostics.Write(
            $"[Sync] enemyDestroyed object='{gameEvent.ObjectName}' score={gameEvent.Score} kills={gameEvent.TotalKills} " +
            $"sceneType={gameEvent.SceneType} sceneIndex={gameEvent.SceneIndex}");

        if (string.Equals(gameEvent.ObjectName, "Seeder", StringComparison.OrdinalIgnoreCase))
        {
            UnlockAchievementOnce(SteamGameConfig.Achievements.FirstSeederDestroyed);
        }
        else if (IsMotherShip(gameEvent.ObjectName))
        {
            UnlockAchievementOnce(SteamGameConfig.Achievements.FirstMothershipDestroyed);
        }

        SyncStats(gameEvent);
        UploadScoreIfImproved(gameEvent.Score);
    }

    private void OnPowerUpCollected(IGameEvent gameEvent)
    {
        if (!steamManager.IsAvailable || IsTutorial(gameEvent))
        {
            SteamDiagnostics.Write($"[Sync] powerUpCollected skipped available={steamManager.IsAvailable} tutorial={IsTutorial(gameEvent)}");
            return;
        }

        SteamDiagnostics.Write(
            $"[Sync] powerUpCollected type={gameEvent.PowerUpType} speedLevel={gameEvent.SpeedPowerUpLevel} score={gameEvent.Score}");

        if (gameEvent.PowerUpType != PowerUpType.Standard || gameEvent.SpeedPowerUpLevel > 0)
        {
            UnlockAchievementOnce(SteamGameConfig.Achievements.SpeedUpgradeCollected);
        }

        SyncStats(gameEvent);
        UploadScoreIfImproved(gameEvent.Score);
    }

    private void OnStyleBonusAwarded(IGameEvent gameEvent)
    {
        if (!steamManager.IsAvailable || IsTutorial(gameEvent))
        {
            SteamDiagnostics.Write($"[Sync] styleBonus skipped available={steamManager.IsAvailable} tutorial={IsTutorial(gameEvent)}");
            return;
        }

        SteamDiagnostics.Write(
            $"[Sync] styleBonus awarded={gameEvent.AwardedScore} hadCollision={gameEvent.HadCollision} score={gameEvent.Score}");

        if (!gameEvent.HadCollision)
        {
            UnlockAchievementOnce(SteamGameConfig.Achievements.CleanLoop);
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
            SteamDiagnostics.Write($"[Sync] sceneCompleted skipped available={steamManager.IsAvailable} tutorial={IsTutorial(gameEvent)}");
            return;
        }

        SteamDiagnostics.Write(
            $"[Sync] sceneCompleted sceneType={gameEvent.SceneType} sceneIndex={gameEvent.SceneIndex} score={gameEvent.Score}");

        if (gameEvent.SceneType == SceneTypes.Game || gameEvent.SceneType == SceneTypes.Simulation)
        {
            UnlockAchievementOnce(SteamGameConfig.Achievements.FirstPlanetCleared);

            if (gameEvent.SceneIndex == 6)
            {
                UnlockAchievementOnce(SteamGameConfig.Achievements.DesertPlanetCleared);
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
            SteamDiagnostics.Write(
                $"[Sync] progressionChanged powerUps={lastPowerUpsCollected}->{gameplay.PowerUpsCollected} " +
                $"speed={lastSpeedPowerUpLevel}->{gameplay.SpeedPowerUpLevel} score={gameplay.Score}");

            if (gameplay.SpeedPowerUpLevel > lastSpeedPowerUpLevel)
            {
                UnlockAchievementOnce(SteamGameConfig.Achievements.SpeedUpgradeCollected);
            }

            SyncStats(gameplay);
            UploadScoreIfImproved(gameplay.Score);
        }

        lastPowerUpsCollected = gameplay.PowerUpsCollected;
        lastSpeedPowerUpLevel = gameplay.SpeedPowerUpLevel;
    }

    private void SyncStats(IGameEvent gameEvent)
    {
        SyncStats(gameEvent.Score, gameEvent.TotalKills, gameEvent.TotalDeaths, gameEvent.SceneIndex);
    }

    private void SyncStats(SteamGameplaySnapshot gameplay)
    {
        SyncStats(gameplay.Score, gameplay.TotalKills, gameplay.TotalDeaths, gameplay.SceneIndex);
    }

    private void SyncStats(long score, int totalKills, int totalDeaths, int sceneIndex)
    {
        bestObservedScore = Math.Max(bestObservedScore, score);

        var nextStats = new SteamSyncedStats(
            BestScore: ToSteamInt(bestObservedScore),
            TotalScore: ToSteamInt(score),
            TotalKills: totalKills,
            Deaths: totalDeaths,
            PlanetsCleared: Math.Max(0, sceneIndex - 1));

        if (lastSyncedStats == nextStats)
        {
            SteamDiagnostics.Write($"[Stats] sync skipped unchanged score={score} kills={totalKills} deaths={totalDeaths} sceneIndex={sceneIndex}");
            return;
        }

        bool bestSet = stats.SetInt(SteamGameConfig.Stats.BestScore, nextStats.BestScore, storeImmediately: false);
        bool totalScoreSet = stats.SetInt(SteamGameConfig.Stats.TotalScore, nextStats.TotalScore, storeImmediately: false);
        bool totalKillsSet = stats.SetInt(SteamGameConfig.Stats.TotalKills, nextStats.TotalKills, storeImmediately: false);
        bool deathsSet = stats.SetInt(SteamGameConfig.Stats.Deaths, nextStats.Deaths, storeImmediately: false);
        bool planetsSet = stats.SetInt(SteamGameConfig.Stats.PlanetsCleared, nextStats.PlanetsCleared, storeImmediately: false);
        bool stored = stats.Store();

        if (bestSet && totalScoreSet && totalKillsSet && deathsSet && planetsSet && stored)
        {
            lastSyncedStats = nextStats;
        }
    }

    private void UnlockAchievementOnce(string achievementId)
    {
        if (!unlockedAchievements.Add(achievementId))
        {
            SteamDiagnostics.Write($"[Achievement] unlock skipped duplicate id='{achievementId}'");
            return;
        }

        if (!achievements.Unlock(achievementId))
        {
            unlockedAchievements.Remove(achievementId);
        }
    }

    private void UploadScoreIfImproved(long score)
    {
        long steamScore = ToSteamInt(score);

        if (steamScore < 0)
        {
            return;
        }

        long scoreToUpload;
        lock (leaderboardUploadGate)
        {
            long bestKnownScore = Math.Max(
                lastConfirmedLeaderboardScore,
                Math.Max(inFlightLeaderboardScore, queuedLeaderboardScore));

            if (steamScore <= bestKnownScore)
            {
                SteamDiagnostics.Write(
                    $"[Leaderboard] upload skipped score={steamScore} bestKnown={bestKnownScore} " +
                    $"confirmed={lastConfirmedLeaderboardScore} inFlight={inFlightLeaderboardScore} queued={queuedLeaderboardScore}");
                return;
            }

            if (leaderboardUploadInFlight)
            {
                queuedLeaderboardScore = steamScore;
                SteamDiagnostics.Write($"[Leaderboard] upload queued score={steamScore} inFlight={inFlightLeaderboardScore}");
                return;
            }

            leaderboardUploadInFlight = true;
            inFlightLeaderboardScore = steamScore;
            scoreToUpload = steamScore;
        }

        _ = UploadLeaderboardScoreAsync(scoreToUpload);
    }

    private async Task UploadLeaderboardScoreAsync(long score)
    {
        bool uploaded = false;

        try
        {
            uploaded = await leaderboards
                .UploadScoreAsync(SteamGameConfig.Leaderboards.GlobalHighScore, ToSteamInt(score))
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            SteamDiagnostics.Write($"[Leaderboard] upload failed score={score} error='{exception.Message}'");
        }

        long nextScoreToUpload = -1;
        lock (leaderboardUploadGate)
        {
            if (uploaded)
            {
                lastConfirmedLeaderboardScore = Math.Max(lastConfirmedLeaderboardScore, score);
            }

            if (inFlightLeaderboardScore == score)
            {
                inFlightLeaderboardScore = -1;
            }

            if (queuedLeaderboardScore > Math.Max(lastConfirmedLeaderboardScore, inFlightLeaderboardScore))
            {
                nextScoreToUpload = queuedLeaderboardScore;
                queuedLeaderboardScore = -1;
                inFlightLeaderboardScore = nextScoreToUpload;
            }
            else
            {
                queuedLeaderboardScore = -1;
                leaderboardUploadInFlight = false;
            }
        }

        if (nextScoreToUpload >= 0)
        {
            _ = UploadLeaderboardScoreAsync(nextScoreToUpload);
        }
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

internal readonly record struct SteamSyncedStats(
    int BestScore,
    int TotalScore,
    int TotalKills,
    int Deaths,
    int PlanetsCleared);

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
