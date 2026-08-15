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
        bool requested = steamManager.IsAvailable && SteamUserStats.RequestCurrentStats();
        SteamDiagnostics.Write($"[Stats] requestCurrentStats steamAvailable={steamManager.IsAvailable} requested={requested}");
        return requested;
    }

    public bool Store()
    {
        bool stored = steamManager.IsAvailable && SteamUserStats.StoreStats();
        SteamDiagnostics.Write($"[Stats] store steamAvailable={steamManager.IsAvailable} stored={stored}");
        return stored;
    }

    public bool TryGetInt(string statId, out int value)
    {
        value = 0;

        if (!CanUseSteam(statId))
        {
            return false;
        }

        bool found = SteamUserStats.GetStat(statId, out value);
        SteamDiagnostics.Write($"[Stats] getInt id='{statId}' found={found} value={value}");
        return found;
    }

    public bool SetInt(string statId, int value, bool storeImmediately = true)
    {
        if (!CanUseSteam(statId))
        {
            SteamDiagnostics.Write($"[Stats] setInt skipped id='{statId}' value={value} steamAvailable={steamManager.IsAvailable}");
            return false;
        }

        bool set = SteamUserStats.SetStat(statId, value);
        bool stored = set && (!storeImmediately || SteamUserStats.StoreStats());
        SteamDiagnostics.Write($"[Stats] setInt id='{statId}' value={value} set={set} stored={stored} storeImmediately={storeImmediately}");
        return set && stored;
    }

    public bool TryGetFloat(string statId, out float value)
    {
        value = 0;

        if (!CanUseSteam(statId))
        {
            return false;
        }

        bool found = SteamUserStats.GetStat(statId, out value);
        SteamDiagnostics.Write($"[Stats] getFloat id='{statId}' found={found} value={value}");
        return found;
    }

    public bool SetFloat(string statId, float value, bool storeImmediately = true)
    {
        if (!CanUseSteam(statId))
        {
            SteamDiagnostics.Write($"[Stats] setFloat skipped id='{statId}' value={value} steamAvailable={steamManager.IsAvailable}");
            return false;
        }

        bool set = SteamUserStats.SetStat(statId, value);
        bool stored = set && (!storeImmediately || SteamUserStats.StoreStats());
        SteamDiagnostics.Write($"[Stats] setFloat id='{statId}' value={value} set={set} stored={stored} storeImmediately={storeImmediately}");
        return set && stored;
    }

    private bool CanUseSteam(string steamId)
    {
        return steamManager.IsAvailable && !string.IsNullOrWhiteSpace(steamId);
    }
}
