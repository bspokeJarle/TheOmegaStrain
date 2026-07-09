namespace SteamIntegration;

public sealed record SteamLeaderboardEntry(ulong SteamId, int GlobalRank, int Score);
