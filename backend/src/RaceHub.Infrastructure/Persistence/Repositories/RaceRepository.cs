using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.Domain.Enums;

namespace RaceHub.Infrastructure.Persistence.Repositories;

public class RaceRepository : IRaceRepository
{
    private readonly AppDbContext _context;

    public RaceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Race race, CancellationToken cancellationToken = default)
    {
        await _context.Races.AddAsync(race, cancellationToken);
    }

    public void AddPlayer(RacePlayer player)
    {
        _context.RacePlayers.Add(player);
    }

    public Task<Race?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Races
            .Include(r => r.Track)
            .Include(r => r.Players)
                .ThenInclude(p => p.User)
            .Include(r => r.Players)
                .ThenInclude(p => p.Car)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<Race?> GetSnapshotByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Races
            .AsNoTracking()
            .Include(r => r.Track)
            .Include(r => r.Players)
                .ThenInclude(p => p.User)
            .Include(r => r.Players)
                .ThenInclude(p => p.Car)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Race>> GetOpenAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Races
            .AsNoTracking()
            .Include(r => r.Track)
            .Include(r => r.Players)
            .Where(r => r.Status == RaceStatus.Waiting)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public void Remove(Race race)
    {
        _context.Races.Remove(race);
    }

    public async Task<IReadOnlyList<Race>> GetActiveRacesForUsersAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var idSet = userIds.Distinct().ToList();

        if (idSet.Count == 0)
        {
            return Array.Empty<Race>();
        }

        return await _context.Races
            .AsNoTracking()
            .Include(r => r.Track)
            .Include(r => r.Players)
            .Where(r => r.Status != RaceStatus.Finished && r.Players.Any(p => idSet.Contains(p.UserId)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Race>> GetWaitingRacesForUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Races
            .Include(r => r.Track)
            .Include(r => r.Players)
                .ThenInclude(p => p.User)
            .Include(r => r.Players)
                .ThenInclude(p => p.Car)
            .Where(r => r.Status == RaceStatus.Waiting && r.Players.Any(p => p.UserId == userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Race>> GetInProgressRacesForUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Races
            .Include(r => r.Track)
            .Include(r => r.Players)
                .ThenInclude(p => p.User)
            .Include(r => r.Players)
                .ThenInclude(p => p.Car)
            .Where(r => (r.Status == RaceStatus.Starting || r.Status == RaceStatus.Running)
                && r.Players.Any(p => p.UserId == userId))
            .ToListAsync(cancellationToken);
    }
}
