using Steamworks;

namespace SteamIntegration;

public sealed class SteamStats
{
    private readonly SteamManager steamManager;

    public SteamStats(SteamManager steamManager)
    {
        this.steamManager = steamManager;
    }

    public bool RequestCurrentStats()
    {
        return steamManager.IsAvailable && SteamUserStats.RequestCurrentStats();
    }

    public bool Store()
    {
        return steamManager.IsAvailable && SteamUserStats.StoreStats();
    }

    public bool TryGetInt(string statId, out int value)
    {
        value = 0;

        if (!CanUseSteam(statId))
        {
            return false;
        }

        return SteamUserStats.GetStat(statId, out value);
    }

    public bool SetInt(string statId, int value, bool storeImmediately = true)
    {
        if (!CanUseSteam(statId) || !SteamUserStats.SetStat(statId, value))
        {
            return false;
        }

        return !storeImmediately || SteamUserStats.StoreStats();
    }

    public bool TryGetFloat(string statId, out float value)
    {
        value = 0;

        if (!CanUseSteam(statId))
        {
            return false;
        }

        return SteamUserStats.GetStat(statId, out value);
    }

    public bool SetFloat(string statId, float value, bool storeImmediately = true)
    {
        if (!CanUseSteam(statId) || !SteamUserStats.SetStat(statId, value))
        {
            return false;
        }

        return !storeImmediately || SteamUserStats.StoreStats();
    }

    private bool CanUseSteam(string steamId)
    {
        return steamManager.IsAvailable && !string.IsNullOrWhiteSpace(steamId);
    }
}
