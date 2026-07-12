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
            return false;
        }

        return SteamUserStats.SetAchievement(achievementId) && SteamUserStats.StoreStats();
    }

    public bool Clear(string achievementId)
    {
        if (!CanUseSteam(achievementId))
        {
            return false;
        }

        return SteamUserStats.ClearAchievement(achievementId) && SteamUserStats.StoreStats();
    }

    public bool TryGetUnlocked(string achievementId, out bool isUnlocked)
    {
        isUnlocked = false;

        if (!CanUseSteam(achievementId))
        {
            return false;
        }

        return SteamUserStats.GetAchievement(achievementId, out isUnlocked);
    }

    private bool CanUseSteam(string steamId)
    {
        return steamManager.IsAvailable && !string.IsNullOrWhiteSpace(steamId);
    }
}
