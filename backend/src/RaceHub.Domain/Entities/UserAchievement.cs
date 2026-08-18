namespace RaceHub.Domain.Entities;

/// <summary>
/// One unlocked, one-time achievement for a user (e.g. "first_win",
/// "win_10_races", "podium_streak_3"). Existence of a row is the unlock —
/// there's no separate "progress" tracking here, that lives implicitly in
/// whatever source table (RaceHistoryEntry, PlayerStatistics) the
/// achievement check queries. Key is a stable string ID rather than an
/// enum so new achievements can be added without a schema change.
/// Deliberately separate from "beat your personal best", which is a
/// recurring notification rather than a one-time unlock — see
/// AchievementsConsumer.
/// </summary>
public class UserAchievement
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Key { get; private set; } = null!;
    public DateTime UnlockedAtUtc { get; private set; } = DateTime.UtcNow;

    private UserAchievement() { }

    public UserAchievement(Guid userId, string key)
    {
        UserId = userId;
        Key = key;
    }
}
