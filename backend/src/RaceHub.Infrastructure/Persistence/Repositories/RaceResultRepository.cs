using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Repositories;

public class RaceResultRepository : IRaceResultRepository
{
    private readonly AppDbContext _context;

    public RaceResultRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RaceResult raceResult, CancellationToken cancellationToken = default)
    {
        await _context.RaceResults.AddAsync(raceResult, cancellationToken);
    }

    public async Task<IReadOnlyList<RaceResult>> GetByRaceIdAsync(Guid raceId, CancellationToken cancellationToken = default)
    {
        return await _context.RaceResults
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.RaceId == raceId)
            .OrderBy(r => r.FinishingPosition)
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetTotalRacesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.RaceResults
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId, cancellationToken);
    }

    public Task<int> GetWinsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.RaceResults
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId && r.FinishingPosition == 1, cancellationToken);
    }

    public async Task<TimeSpan?> GetBestLapTimeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.RaceResults
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.BestLapTime != null)
            .OrderBy(r => r.BestLapTime)
            .Select(r => r.BestLapTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RaceResult>> GetRecentAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        return await _context.RaceResults
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Include(r => r.Race)
                .ThenInclude(race => race.Track)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
