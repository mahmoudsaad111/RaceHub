using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Persistence;
namespace RaceHub.Infrastructure.Persistence.Repositories;
public class UserRewardRepository : IUserRewardRepository
{
    private readonly AppDbContext _context;
    public UserRewardRepository(AppDbContext context) => _context = context;

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
}