namespace RaceHub.Domain.Entities;

public class RaceResult : BaseEntity
{
    public Guid RaceId { get; private set; }

    public Guid UserId { get; private set; }

    public int FinishingPosition { get; private set; }

    public TimeSpan TotalRaceTime { get; private set; }

    public TimeSpan? BestLapTime { get; private set; }

    public int ExperienceEarned { get; private set; }

    public int CoinsEarned { get; private set; }

    public Race Race { get; private set; } = null!;

    public User User { get; private set; } = null!;

    private RaceResult() { }

    public RaceResult(
        Guid raceId,
        Guid userId,
        int finishingPosition,
        TimeSpan totalRaceTime,
        TimeSpan? bestLapTime,
        int experienceEarned,
        int coinsEarned)
    {
        RaceId = raceId;
        UserId = userId;
        FinishingPosition = finishingPosition;
        TotalRaceTime = totalRaceTime;
        BestLapTime = bestLapTime;
        ExperienceEarned = experienceEarned;
        CoinsEarned = coinsEarned;
    }
}