namespace RaceHub.Contracts.Achievements;

public record AchievementDefinition(string Key, string Title, string Description);

/// <summary>
/// The achievement catalog. Shared between RaceHub.AchievementsWorker
/// (which evaluates unlocks off race.finished events) and RaceHub.API
/// (which lists locked/unlocked badges on the profile), so the two can
/// never disagree about what exists. Keys are stable strings stored on
/// UserAchievement rows — new achievements are added here, no schema
/// change. Anything progress-flavored ("win 10") is a one-time unlock;
/// recurring events like beating a personal best are deliberately NOT
/// here — those are one-off notifications, not badges (see
/// UserAchievement's doc comment).
/// </summary>
public static class AchievementDefinitions
{
    public const string FirstRace = "first_race";
    public const string Races10 = "races_10";
    public const string Races50 = "races_50";
    public const string FirstWin = "first_win";
    public const string Wins10 = "wins_10";
    public const string PodiumStreak3 = "podium_streak_3";

    public static readonly IReadOnlyList<AchievementDefinition> All = new[]
    {
        new AchievementDefinition(FirstRace, "Getting Started", "Complete your first race."),
        new AchievementDefinition(Races10, "Regular", "Complete 10 races."),
        new AchievementDefinition(Races50, "Veteran", "Complete 50 races."),
        new AchievementDefinition(FirstWin, "Winner Winner", "Take first place for the first time."),
        new AchievementDefinition(Wins10, "Dominator", "Win 10 races."),
        new AchievementDefinition(PodiumStreak3, "On a Roll", "Finish on the podium (top 3) three races in a row."),
    };

    public static AchievementDefinition? Find(string key) =>
        All.FirstOrDefault(a => a.Key == key);
}
