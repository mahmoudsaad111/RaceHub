namespace RaceHub.Domain.Entities;

/// <summary>
/// Flat XP-per-level curve shared by every consumer that needs to know a
/// user's level from their raw Experience total — GetProfileQueryHandler
/// (read) and RewardWorker (write, to detect a level-up the moment XP is
/// credited). Centralized here instead of duplicated in both places so the
/// curve can only ever drift out of sync with itself, not with a second
/// copy — swap the curve later without hunting down every place that used
/// to inline "/ 1000 + 1".
/// </summary>
public static class LevelCalculator
{
    public const int XpPerLevel = 1000;

    public static int GetLevel(int experience) => (experience / XpPerLevel) + 1;

    public static int GetXpIntoLevel(int experience) => experience % XpPerLevel;
}
