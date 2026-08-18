using Microsoft.EntityFrameworkCore;
using RaceHub.Application.DTOs.Leaderboards;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Persistence;

namespace RaceHub.Infrastructure.Persistence.Repositories;

/// <summary>
/// Global scope reads PlayerStatistics — the materialized read model
/// RankingWorker maintains asynchronously off race.finished events —
/// instead of aggregating RaceResult live, so this is an
/// eventually-consistent, cheap indexed read. Weekly and track scopes
/// aggregate RaceHistoryEntry (StatisticsWorker's read model) the same
/// way. Neither path touches the operational Race/RaceResult tables at
/// all: that's the read/write separation the three workers exist for.
/// </summary>
public class LeaderboardRepository : ILeaderboardRepository
{
    private const int MaxEntries = 100;
    private const int MaxTrackEntries = 50;

    private readonly AppDbContext _context;

    public LeaderboardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
        string scope,
        Guid? trackId = null,
        CancellationToken cancellationToken = default)
    {
        return scope switch
        {
            "global" => await GetGlobalAsync(cancellationToken),
            "weekly" => await GetWeeklyAsync(cancellationToken),
            "track" => await GetForTrackAsync(trackId!.Value, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scope)),
        };
    }

    /// <summary>
    /// The headline leaderboard: ordered by RankingWorker's Elo-style
    /// RatingPoints, ties broken by total wins. A PlayerStatistics row
    /// exists only once RankingWorker has processed that user's first
    /// race.finished event — users who haven't raced yet simply don't
    /// appear, which is correct for a rating ladder.
    /// </summary>
    private async Task<IReadOnlyList<LeaderboardEntryDto>> GetGlobalAsync(CancellationToken ct)
    {
        var entries = await _context.PlayerStatistics
            .AsNoTracking()
            .OrderByDescending(s => s.RatingPoints)
            .ThenByDescending(s => s.TotalWins)
            .Take(MaxEntries)
            .Select(s => new
            {
                s.UserId,
                s.TotalWins,
                s.TotalRaces,
                s.BestTimeMs,
                s.RatingPoints
            })
            .ToListAsync(ct);

        var names = await GetDisplayNamesAsync(entries.Select(e => e.UserId), ct);

        return entries
            .Select(e => new LeaderboardEntryDto(
                e.UserId,
                names.GetValueOrDefault(e.UserId, "Unknown"),
                e.TotalWins,
                e.TotalRaces,
                e.BestTimeMs is int ms ? TimeSpan.FromMilliseconds(ms) : null,
                e.RatingPoints))
            .ToList();
    }

    private async Task<IReadOnlyList<LeaderboardEntryDto>> GetWeeklyAsync(CancellationToken ct)
    {
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        var entries = await _context.RaceHistoryEntries
            .AsNoTracking()
            .Where(e => e.RecordedAtUtc >= weekAgo)
            .GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Wins = g.Count(e => e.Position == 1),
                TotalRaces = g.Count(),
                BestTimeMs = g.Min(e => e.FinishTimeMs)
            })
            .OrderByDescending(e => e.Wins)
            .ThenBy(e => e.BestTimeMs)
            .Take(MaxEntries)
            .ToListAsync(ct);

        var names = await GetDisplayNamesAsync(entries.Select(e => e.UserId), ct);

        return entries
            .Select(e => new LeaderboardEntryDto(
                e.UserId,
                names.GetValueOrDefault(e.UserId, "Unknown"),
                e.Wins,
                e.TotalRaces,
                TimeSpan.FromMilliseconds(e.BestTimeMs)))
            .ToList();
    }

    /// <summary>"Fastest on this track" — best FinishTimeMs per user.</summary>
    private async Task<IReadOnlyList<LeaderboardEntryDto>> GetForTrackAsync(Guid trackId, CancellationToken ct)
    {
        var entries = await _context.RaceHistoryEntries
            .AsNoTracking()
            .Where(e => e.TrackId == trackId)
            .GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Wins = g.Count(e => e.Position == 1),
                TotalRaces = g.Count(),
                BestTimeMs = g.Min(e => e.FinishTimeMs)
            })
            .OrderBy(e => e.BestTimeMs)
            .Take(MaxTrackEntries)
            .ToListAsync(ct);

        var names = await GetDisplayNamesAsync(entries.Select(e => e.UserId), ct);

        return entries
            .Select(e => new LeaderboardEntryDto(
                e.UserId,
                names.GetValueOrDefault(e.UserId, "Unknown"),
                e.Wins,
                e.TotalRaces,
                TimeSpan.FromMilliseconds(e.BestTimeMs)))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.ToList();

        return await _context.Users
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }
}
