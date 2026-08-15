namespace TheOmegaStrain.Steam;

public sealed record SteamLeaderboardEntry(ulong SteamId, int GlobalRank, int Score);
