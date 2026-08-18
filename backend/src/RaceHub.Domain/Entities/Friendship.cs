using RaceHub.Domain.Enums;

namespace RaceHub.Domain.Entities;

/// <summary>
/// Directed friend request/relationship between two users. Requester sends
/// the request; Addressee accepts/declines it. Once Accepted, the
/// relationship reads as mutual (GetFriends checks both directions) — the
/// direction only matters for who initiated it and who's allowed to
/// accept/decline.
/// </summary>
public class Friendship : BaseEntity
{
    public Guid RequesterId { get; private set; }

    public Guid AddresseeId { get; private set; }

    public FriendshipStatus Status { get; private set; }

    public DateTime? RespondedAtUtc { get; private set; }

    public User Requester { get; private set; } = null!;

    public User Addressee { get; private set; } = null!;

    private Friendship() { }

    public Friendship(Guid requesterId, Guid addresseeId)
    {
        if (requesterId == addresseeId)
        {
            throw new InvalidOperationException("A user cannot friend themselves.");
        }

        RequesterId = requesterId;
        AddresseeId = addresseeId;
        Status = FriendshipStatus.Pending;
    }

    public void Accept()
    {
        Status = FriendshipStatus.Accepted;
        RespondedAtUtc = DateTime.UtcNow;
    }

    public void Decline()
    {
        Status = FriendshipStatus.Declined;
        RespondedAtUtc = DateTime.UtcNow;
    }
}
