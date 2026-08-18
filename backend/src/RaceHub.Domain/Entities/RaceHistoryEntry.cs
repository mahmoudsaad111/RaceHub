namespace RaceHub.Domain.Entities ;
public class RaceHistoryEntry
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid RaceId { get; private set; }
    public Guid TrackId { get; private set; }
    public int Position { get; private set; }
    public int FinishTimeMs { get; private set; }
    public DateTime RecordedAtUtc { get; private set; } = DateTime.UtcNow;

    private RaceHistoryEntry() { }
    public RaceHistoryEntry(Guid userId, Guid raceId, Guid trackId, int position, int finishTimeMs)
    {
        UserId = userId; RaceId = raceId; TrackId = trackId;
        Position = position; FinishTimeMs = finishTimeMs;
    }
}