using RaceHub.Application.DTOs.Leaderboards;

namespace RaceHub.Application.Interfaces.Persistence;

public interface ILeaderboardRepository
{
    /// <summary>
    /// scope: "global" (RatingPoints from PlayerStatistics, the
    /// RankingWorker-maintained read model), "weekly" (wins in the last 7
    /// days from RaceHistoryEntry) or "track" (TrackId required — best
    /// time on that track from RaceHistoryEntry).
    /// </summary>
    Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
        string scope,
        Guid? trackId = null,
        CancellationToken cancellationToken = default);
}
