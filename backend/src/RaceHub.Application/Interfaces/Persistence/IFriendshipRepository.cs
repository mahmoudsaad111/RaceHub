using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Persistence;

public interface IFriendshipRepository
{
    Task AddAsync(Friendship friendship, CancellationToken cancellationToken = default);

    Task<Friendship?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Finds any relationship between the two users, in either direction.</summary>
    Task<Friendship?> GetBetweenAsync(Guid userIdA, Guid userIdB, CancellationToken cancellationToken = default);

    /// <summary>Accepted friendships involving the user, in either direction.</summary>
    Task<IReadOnlyList<Friendship>> GetAcceptedForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Pending requests where the user is the addressee (i.e. awaiting their response).</summary>
    Task<IReadOnlyList<Friendship>> GetPendingIncomingAsync(Guid userId, CancellationToken cancellationToken = default);
}
