using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.Domain.Enums;

namespace RaceHub.Infrastructure.Persistence.Repositories;

public class FriendshipRepository : IFriendshipRepository
{
    private readonly AppDbContext _context;

    public FriendshipRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Friendship friendship, CancellationToken cancellationToken = default)
    {
        await _context.Friendships.AddAsync(friendship, cancellationToken);
    }

    public Task<Friendship?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Tracked (no AsNoTracking) — callers mutate the entity
        // (Accept/Decline) and rely on SaveChangesAsync to persist it.
        return _context.Friendships
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public Task<Friendship?> GetBetweenAsync(
        Guid userIdA,
        Guid userIdB,
        CancellationToken cancellationToken = default)
    {
        return _context.Friendships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                f => (f.RequesterId == userIdA && f.AddresseeId == userIdB) ||
                     (f.RequesterId == userIdB && f.AddresseeId == userIdA),
                cancellationToken);
    }

    public async Task<IReadOnlyList<Friendship>> GetAcceptedForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Friendships
            .AsNoTracking()
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                        (f.RequesterId == userId || f.AddresseeId == userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Friendship>> GetPendingIncomingAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Friendships
            .AsNoTracking()
            .Include(f => f.Requester)
            .Where(f => f.AddresseeId == userId && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
