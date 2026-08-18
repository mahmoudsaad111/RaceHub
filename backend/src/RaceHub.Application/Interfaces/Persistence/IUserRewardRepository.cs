using RaceHub.Domain.Entities;
namespace RaceHub.Application.Interfaces.Persistence;
public interface IUserRewardRepository
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);
}
