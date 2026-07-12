using Steamworks;

namespace SteamIntegration;

public sealed class SteamLeaderboards
{
    private readonly SteamManager steamManager;
    private readonly List<object> pendingCallResults = [];

    public SteamLeaderboards(SteamManager steamManager)
    {
        this.steamManager = steamManager;
    }

    public Task<SteamLeaderboard_t?> FindAsync(string leaderboardName)
    {
        if (!CanUseSteam(leaderboardName))
        {
            return Task.FromResult<SteamLeaderboard_t?>(null);
        }

        var completion = new TaskCompletionSource<SteamLeaderboard_t?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var call = SteamUserStats.FindLeaderboard(leaderboardName);
        CallResult<LeaderboardFindResult_t>? result = null;
        result = CallResult<LeaderboardFindResult_t>.Create((leaderboard, failure) =>
        {
            pendingCallResults.Remove(result);

            if (failure || !Convert.ToBoolean(leaderboard.m_bLeaderboardFound))
            {
                completion.TrySetResult(null);
                return;
            }

            completion.TrySetResult(leaderboard.m_hSteamLeaderboard);
        });

        pendingCallResults.Add(result);
        result.Set(call);
        return completion.Task;
    }

    public Task<bool> UploadScoreAsync(SteamLeaderboard_t leaderboard, int score)
    {
        if (!steamManager.IsAvailable)
        {
            return Task.FromResult(false);
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var call = SteamUserStats.UploadLeaderboardScore(
            leaderboard,
            ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
            score,
            [],
            0);

        CallResult<LeaderboardScoreUploaded_t>? result = null;
        result = CallResult<LeaderboardScoreUploaded_t>.Create((uploaded, failure) =>
        {
            pendingCallResults.Remove(result);
            completion.TrySetResult(!failure && Convert.ToBoolean(uploaded.m_bSuccess));
        });

        pendingCallResults.Add(result);
        result.Set(call);
        return completion.Task;
    }

    public Task<IReadOnlyList<SteamLeaderboardEntry>> DownloadGlobalScoresAsync(
        SteamLeaderboard_t leaderboard,
        int firstEntry = 1,
        int lastEntry = 10)
    {
        if (!steamManager.IsAvailable)
        {
            return Task.FromResult<IReadOnlyList<SteamLeaderboardEntry>>([]);
        }

        var completion = new TaskCompletionSource<IReadOnlyList<SteamLeaderboardEntry>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var call = SteamUserStats.DownloadLeaderboardEntries(
            leaderboard,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
            firstEntry,
            lastEntry);

        CallResult<LeaderboardScoresDownloaded_t>? result = null;
        result = CallResult<LeaderboardScoresDownloaded_t>.Create((downloaded, failure) =>
        {
            pendingCallResults.Remove(result);

            if (failure)
            {
                completion.TrySetResult([]);
                return;
            }

            var entries = new List<SteamLeaderboardEntry>(downloaded.m_cEntryCount);
            var details = Array.Empty<int>();

            for (var index = 0; index < downloaded.m_cEntryCount; index++)
            {
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(
                        downloaded.m_hSteamLeaderboardEntries,
                        index,
                        out var entry,
                        details,
                        details.Length))
                {
                    continue;
                }

                entries.Add(new SteamLeaderboardEntry(
                    entry.m_steamIDUser.m_SteamID,
                    entry.m_nGlobalRank,
                    entry.m_nScore));
            }

            completion.TrySetResult(entries);
        });

        pendingCallResults.Add(result);
        result.Set(call);
        return completion.Task;
    }

    public async Task<bool> UploadScoreAsync(string leaderboardName, int score)
    {
        var leaderboard = await FindAsync(leaderboardName).ConfigureAwait(false);
        return leaderboard.HasValue && await UploadScoreAsync(leaderboard.Value, score).ConfigureAwait(false);
    }

    private bool CanUseSteam(string steamId)
    {
        return steamManager.IsAvailable && !string.IsNullOrWhiteSpace(steamId);
    }
}
