
using Microsoft.AspNetCore.Identity;

namespace RaceHub.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = null!;

    public int Experience { get; private set; }

    public int Coins { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public ICollection<RacePlayer> RaceParticipations { get; private set; }
        = new List<RacePlayer>();

    public ICollection<RaceResult> RaceResults { get; private set; }
        = new List<RaceResult>();
    public ICollection<RefreshToken> RefreshTokens { get; private set; }
= new List<RefreshToken>();
    // User.cs — add this method
    public void AddReward(int coins, int experience)
    {
        Coins += coins;
        Experience += experience;
    }

    /// <summary>
    /// Returns false (spending nothing) rather than throwing when the
    /// balance is insufficient — insufficient funds is an expected,
    /// recoverable outcome a command handler checks and turns into a
    /// normal Result.Failure, not an exceptional one.
    /// </summary>
    public bool SpendCoins(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (Coins < amount) return false;

        Coins -= amount;
        return true;
    }
}