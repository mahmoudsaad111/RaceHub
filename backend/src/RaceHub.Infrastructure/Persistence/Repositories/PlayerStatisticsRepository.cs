using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Repositories;
public class PlayerStatisticsRepository : IPlayerStatisticsRepository
{
    private readonly AppDbContext _context;
    public PlayerStatisticsRepository(AppDbContext context) => _context = context;

    public Task<PlayerStatistics?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _context.Set<PlayerStatistics>().FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public Task AddAsync(PlayerStatistics stats, CancellationToken ct = default)
        => _context.Set<PlayerStatistics>().AddAsync(stats, ct).AsTask();
}