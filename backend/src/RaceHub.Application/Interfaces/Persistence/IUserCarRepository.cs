using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Persistence;

public interface IUserCarRepository
{
    Task<bool> OwnsAsync(Guid userId, Guid carId, CancellationToken ct = default);

    /// <summary>Every car this user has purchased — used by GetCarsQueryHandler to flag each car's "owned" state for the current user in one query instead of one OwnsAsync call per car.</summary>
    Task<IReadOnlySet<Guid>> GetOwnedCarIdsAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(UserCar userCar, CancellationToken ct = default);
}
