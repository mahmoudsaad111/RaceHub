namespace RaceHub.Domain.Entities;

public class Track : BaseEntity
{
    public string Name { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public int TotalLaps { get; private set; }

    public int Difficulty { get; private set; }

    public bool IsActive { get; private set; }

    public ICollection<TrackCheckpoint> Checkpoints { get; private set; }
        = new List<TrackCheckpoint>();

    public ICollection<Race> Races { get; private set; }
        = new List<Race>();

    private Track() { }

    public Track(
        string name,
        string description,
        int totalLaps,
        int difficulty)
    {
        Name = name;
        Description = description;
        TotalLaps = totalLaps;
        Difficulty = difficulty;
        IsActive = true;
    }
}