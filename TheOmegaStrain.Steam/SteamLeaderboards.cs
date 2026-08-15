using Steamworks;

namespace TheOmegaStrain.Steam;

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
            SteamDiagnostics.Write($"[Leaderboard] find skipped name='{leaderboardName}' steamAvailable={steamManager.IsAvailable}");
            return Task.FromResult<SteamLeaderboard_t?>(null);
        }

        SteamDiagnostics.Write($"[Leaderboard] find requested name='{leaderboardName}'");
        var completion = new TaskCompletionSource<SteamLeaderboard_t?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var call = SteamUserStats.FindLeaderboard(leaderboardName);
        CallResult<LeaderboardFindResult_t>? result = null;
        result = CallResult<LeaderboardFindResult_t>.Create((leaderboard, failure) =>
        {
            pendingCallResults.Remove(result);

            if (failure || !Convert.ToBoolean(leaderboard.m_bLeaderboardFound))
            {
                SteamDiagnostics.Write($"[Leaderboard] find completed name='{leaderboardName}' failure={failure} found={Convert.ToBoolean(leaderboard.m_bLeaderboardFound)}");
                completion.TrySetResult(null);
                return;
            }

            SteamDiagnostics.Write($"[Leaderboard] find completed name='{leaderboardName}' failure={failure} found=True handle={leaderboard.m_hSteamLeaderboard.m_SteamLeaderboard}");
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
            SteamDiagnostics.Write($"[Leaderboard] upload skipped score={score} steamAvailable=False");
            return Task.FromResult(false);
        }

        SteamDiagnostics.Write($"[Leaderboard] upload requested handle={leaderboard.m_SteamLeaderboard} score={score}");
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
            bool success = !failure && Convert.ToBoolean(uploaded.m_bSuccess);
            SteamDiagnostics.Write($"[Leaderboard] upload completed handle={leaderboard.m_SteamLeaderboard} score={score} failure={failure} success={success}");
            completion.TrySetResult(success);
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
            SteamDiagnostics.Write("[Leaderboard] download skipped steamAvailable=False");
            return Task.FromResult<IReadOnlyList<SteamLeaderboardEntry>>([]);
        }

        SteamDiagnostics.Write($"[Leaderboard] download requested handle={leaderboard.m_SteamLeaderboard} first={firstEntry} last={lastEntry}");
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
                SteamDiagnostics.Write($"[Leaderboard] download completed handle={leaderboard.m_SteamLeaderboard} failure=True");
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

            SteamDiagnostics.Write($"[Leaderboard] download completed handle={leaderboard.m_SteamLeaderboard} failure=False entries={entries.Count}");
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
