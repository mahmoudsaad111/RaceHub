using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Repositories;

public class UserCarRepository : IUserCarRepository
{
    private readonly AppDbContext _context;
    public UserCarRepository(AppDbContext context) => _context = context;

    public Task<bool> OwnsAsync(Guid userId, Guid carId, CancellationToken ct = default) =>
        _context.UserCars.AnyAsync(uc => uc.UserId == userId && uc.CarId == carId, ct);

    public async Task<IReadOnlySet<Guid>> GetOwnedCarIdsAsync(Guid userId, CancellationToken ct = default) =>
        (await _context.UserCars
            .Where(uc => uc.UserId == userId)
            .Select(uc => uc.CarId)
            .ToListAsync(ct))
        .ToHashSet();

    public Task AddAsync(UserCar userCar, CancellationToken ct = default) =>
        _context.UserCars.AddAsync(userCar, ct).AsTask();
}
