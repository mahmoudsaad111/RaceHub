using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Persistence;

public interface ICarRepository
{
    Task<IReadOnlyList<Car>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<Car?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
