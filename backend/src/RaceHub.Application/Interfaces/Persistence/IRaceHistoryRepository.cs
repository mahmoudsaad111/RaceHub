using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Persistence;

public interface IRaceHistoryRepository
{
    Task AddAsync(RaceHistoryEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Most recent entries for a user, newest first, offset-paginated.
    /// Returns the page plus the total row count (for page-count display),
    /// not just the page itself.
    /// </summary>
    Task<(IReadOnlyList<RaceHistoryEntry> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>This user's fastest FinishTimeMs on each track they've raced, keyed by TrackId.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetPersonalBestsByTrackAsync(
        Guid userId, CancellationToken ct = default);

    /// <summary>Best FinishTimeMs per user for one track, best-first.</summary>
    Task<IReadOnlyList<(Guid UserId, int BestTimeMs)>> GetTrackLeaderboardAsync(
        Guid trackId, int take, CancellationToken ct = default);

    /// <summary>Most recent entries for a user across all tracks — used to check streak-style achievements.</summary>
    Task<IReadOnlyList<RaceHistoryEntry>> GetRecentByUserIdAsync(
        Guid userId, int count, CancellationToken ct = default);

    /// <summary>
    /// Win count for a user, excluding one specific race — used by
    /// AchievementsWorker, which processes the same RaceFinished event
    /// concurrently with (not after) StatisticsWorker on an independent
    /// queue. Whether *this* race's own history row has landed yet by the
    /// time AchievementsWorker reads is a race condition; excluding it
    /// explicitly and adding the current result in code makes the count
    /// correct either way instead of silently double-counting.
    /// </summary>
    Task<int> GetWinCountExcludingRaceAsync(Guid userId, Guid excludingRaceId, CancellationToken ct = default);

    /// <summary>Same exclude-the-current-race reasoning as GetWinCountExcludingRaceAsync, for "did I just beat my personal best" checks.</summary>
    Task<int?> GetBestTimeForTrackExcludingRaceAsync(Guid userId, Guid trackId, Guid excludingRaceId, CancellationToken ct = default);
}
