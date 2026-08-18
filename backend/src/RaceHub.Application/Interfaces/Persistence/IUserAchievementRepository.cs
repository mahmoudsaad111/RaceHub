using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Persistence;

public interface IUserAchievementRepository
{
    Task<IReadOnlyList<UserAchievement>> GetAllForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The (UserId, Key) pair is uniquely indexed — if the same achievement
    /// is somehow evaluated twice, the second insert throws a
    /// DbUpdateException which IdempotentConsumer treats as a poison
    /// message and retries/DLQs, rather than silently duplicating badges.
    /// </summary>
    Task AddAsync(UserAchievement achievement, CancellationToken ct = default);
}
