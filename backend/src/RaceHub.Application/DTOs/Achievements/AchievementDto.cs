namespace RaceHub.Application.DTOs.Achievements;

/// <summary>
/// The full catalog from AchievementDefinitions with this user's unlock
/// status merged in — locked badges are included (greyed out in the UI),
/// not filtered out, so players can see what to chase next.
/// </summary>
public record AchievementDto(
    string Key,
    string Title,
    string Description,
    bool Unlocked,
    DateTime? UnlockedAtUtc);
