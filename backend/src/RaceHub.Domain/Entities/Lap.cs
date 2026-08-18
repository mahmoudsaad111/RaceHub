namespace RaceHub.Domain.Entities;

public class Lap : BaseEntity
{
    public Guid RacePlayerId { get; private set; }

    public int LapNumber { get; private set; }

    public TimeSpan LapTime { get; private set; }

    public DateTime CompletedAtUtc { get; private set; }

    public RacePlayer RacePlayer { get; private set; } = null!;

    private Lap() { }

    public Lap(
        Guid racePlayerId,
        int lapNumber,
        TimeSpan lapTime)
    {
        RacePlayerId = racePlayerId;
        LapNumber = lapNumber;
        LapTime = lapTime;
        CompletedAtUtc = DateTime.UtcNow;
    }
}