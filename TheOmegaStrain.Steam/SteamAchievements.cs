using Steamworks;

namespace SteamIntegration;

public sealed class SteamAchievements
{
    private readonly SteamManager steamManager;

    public SteamAchievements(SteamManager steamManager)
    {
        this.steamManager = steamManager;
    }

    public bool Unlock(string achievementId)
    {
        if (!CanUseSteam(achievementId))
        {
            SteamDiagnostics.Write($"[Achievement] unlock skipped id='{achievementId}' steamAvailable={steamManager.IsAvailable}");
            return false;
        }

        bool set = SteamUserStats.SetAchievement(achievementId);
        bool stored = set && SteamUserStats.StoreStats();
        SteamDiagnostics.Write($"[Achievement] unlock id='{achievementId}' set={set} stored={stored}");
        return set && stored;
    }

    public bool Clear(string achievementId)
    {
        if (!CanUseSteam(achievementId))
        {
            SteamDiagnostics.Write($"[Achievement] clear skipped id='{achievementId}' steamAvailable={steamManager.IsAvailable}");
            return false;
        }

        bool cleared = SteamUserStats.ClearAchievement(achievementId);
        bool stored = cleared && SteamUserStats.StoreStats();
        SteamDiagnostics.Write($"[Achievement] clear id='{achievementId}' cleared={cleared} stored={stored}");
        return cleared && stored;
    }

    public bool TryGetUnlocked(string achievementId, out bool isUnlocked)
    {
        isUnlocked = false;

        if (!CanUseSteam(achievementId))
        {
            SteamDiagnostics.Write($"[Achievement] get skipped id='{achievementId}' steamAvailable={steamManager.IsAvailable}");
            return false;
        }

        bool found = SteamUserStats.GetAchievement(achievementId, out isUnlocked);
        SteamDiagnostics.Write($"[Achievement] get id='{achievementId}' found={found} unlocked={isUnlocked}");
        return found;
    }

    private bool CanUseSteam(string steamId)
    {
        return steamManager.IsAvailable && !string.IsNullOrWhiteSpace(steamId);
    }
}
