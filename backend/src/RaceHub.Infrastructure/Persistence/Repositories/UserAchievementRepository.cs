using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Persistence;

namespace RaceHub.Infrastructure.Persistence.Repositories;

public class UserAchievementRepository : IUserAchievementRepository
{
    private readonly AppDbContext _context;

    public UserAchievementRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<UserAchievement>> GetAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserAchievements
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task AddAsync(UserAchievement achievement, CancellationToken ct = default)
    {
        await _context.UserAchievements.AddAsync(achievement, ct);
    }
}
