using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Repositories;

public class RaceHistoryRepository : IRaceHistoryRepository
{
    private readonly AppDbContext _context;

    public RaceHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RaceHistoryEntry entry, CancellationToken ct = default)
    {
        await _context.Set<RaceHistoryEntry>().AddAsync(entry, ct);
    }

    public async Task<(IReadOnlyList<RaceHistoryEntry> Items, int TotalCount)> GetPagedByUserIdAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Set<RaceHistoryEntry>()
            .AsNoTracking()
            .Where(e => e.UserId == userId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.RecordedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    /// <summary>
    /// This user's fastest FinishTimeMs on each track they've raced,
    /// keyed by TrackId — powers the "your PB: 1:23.45" hint on the track
    /// picker. Sourced from RaceHistoryEntry (StatisticsWorker's output)
    /// rather than querying RaceResult directly, so this is a genuinely
    /// separate, async-populated read path, not just RaceResult renamed.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> GetPersonalBestsByTrackAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await _context.Set<RaceHistoryEntry>()
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .GroupBy(e => e.TrackId)
            .Select(g => new { TrackId = g.Key, BestTimeMs = g.Min(e => e.FinishTimeMs) })
            .ToDictionaryAsync(x => x.TrackId, x => x.BestTimeMs, ct);
    }

    /// <summary>
    /// Best FinishTimeMs per user for a single track, best-first — powers
    /// the per-track leaderboard ("fastest on Beach Track").
    /// </summary>
    public async Task<IReadOnlyList<(Guid UserId, int BestTimeMs)>> GetTrackLeaderboardAsync(
        Guid trackId, int take, CancellationToken ct = default)
    {
        var rows = await _context.Set<RaceHistoryEntry>()
            .AsNoTracking()
            .Where(e => e.TrackId == trackId)
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, BestTimeMs = g.Min(e => e.FinishTimeMs) })
            .OrderBy(x => x.BestTimeMs)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(r => (r.UserId, r.BestTimeMs)).ToList();
    }

    /// <summary>
    /// Most recent entries for a user across all tracks, newest first —
    /// used by AchievementsWorker to check "3 podiums in a row" without
    /// needing every past race.
    /// </summary>
    public async Task<IReadOnlyList<RaceHistoryEntry>> GetRecentByUserIdAsync(
        Guid userId, int count, CancellationToken ct = default)
    {
        return await _context.Set<RaceHistoryEntry>()
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.RecordedAtUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<int> GetWinCountExcludingRaceAsync(Guid userId, Guid excludingRaceId, CancellationToken ct = default)
    {
        return await _context.Set<RaceHistoryEntry>()
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId && e.Position == 1 && e.RaceId != excludingRaceId, ct);
    }

    public async Task<int?> GetBestTimeForTrackExcludingRaceAsync(Guid userId, Guid trackId, Guid excludingRaceId, CancellationToken ct = default)
    {
        var times = await _context.Set<RaceHistoryEntry>()
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.TrackId == trackId && e.RaceId != excludingRaceId)
            .Select(e => (int?)e.FinishTimeMs)
            .ToListAsync(ct);

        return times.Count > 0 ? times.Min() : null;
    }
}
