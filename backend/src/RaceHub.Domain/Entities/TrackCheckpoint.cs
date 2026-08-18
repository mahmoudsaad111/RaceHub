namespace RaceHub.Domain.Entities;

public class TrackCheckpoint : BaseEntity
{
    public Guid TrackId { get; private set; }

    public int Sequence { get; private set; }

    public decimal PositionX { get; private set; }

    public decimal PositionY { get; private set; }

    public decimal Width { get; private set; }

    public Track Track { get; private set; } = null!;

    private TrackCheckpoint() { }

    public TrackCheckpoint(
        Guid trackId,
        int sequence,
        decimal positionX,
        decimal positionY,
        decimal width)
    {
        TrackId = trackId;
        Sequence = sequence;
        PositionX = positionX;
        PositionY = positionY;
        Width = width;
    }
}