using RaceHub.Domain.Entities;
namespace RaceHub.Application.Interfaces.Persistence;
public interface IPlayerStatisticsRepository
{
    Task<PlayerStatistics?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(PlayerStatistics stats, CancellationToken ct = default);
}