namespace RaceHub.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public User User { get; private set; } = null!;

    public bool IsActive =>
        RevokedAtUtc is null &&
        ExpiresAtUtc > DateTime.UtcNow;

    private RefreshToken() { }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void Revoke()
    {
        RevokedAtUtc = DateTime.UtcNow;
    }
}