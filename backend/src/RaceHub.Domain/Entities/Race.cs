using RaceHub.Domain.Enums;

namespace RaceHub.Domain.Entities;

public class Race : BaseEntity
{
    public Guid TrackId { get; private set; }

    public Guid HostUserId { get; private set; }

    public RaceStatus Status { get; private set; }

    public int MaxPlayers { get; private set; }

    public int TotalLaps { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? FinishedAtUtc { get; private set; }

    public Track Track { get; private set; } = null!;

    public ICollection<RacePlayer> Players { get; private set; }
        = new List<RacePlayer>();

    public ICollection<RaceResult> Results { get; private set; }
        = new List<RaceResult>();

    private Race() { }

    public Race(
        Guid trackId,
        Guid hostUserId,
        int maxPlayers,
        int totalLaps)
    {
        TrackId = trackId;
        HostUserId = hostUserId;
        MaxPlayers = maxPlayers;
        TotalLaps = totalLaps;
        Status = RaceStatus.Waiting;
    }

    /// <summary>
    /// Adds a player to the room. Throws if the race has already started,
    /// is full, or the user already joined — callers (the command handler)
    /// translate these into the appropriate Result failure instead of
    /// letting the exception surface, since these are expected/user-facing
    /// conditions rather than bugs.
    /// </summary>
    public RacePlayer AddPlayer(Guid userId, Guid carId)
    {
        if (Status != RaceStatus.Waiting)
            throw new InvalidOperationException("This race has already started.");

        if (Players.Count >= MaxPlayers)
            throw new InvalidOperationException("This race is full.");

        if (Players.Any(p => p.UserId == userId))
            throw new InvalidOperationException("You have already joined this race.");

        var player = new RacePlayer(Id, userId, carId);
        Players.Add(player);
        return player;
    }

    /// <summary>
    /// Removes a player from the room. Relies on EF Core's default orphan
    /// delete for required relationships — removing a RacePlayer from this
    /// tracked collection deletes its row on SaveChanges since RaceId is a
    /// non-nullable FK (see RacePlayerConfiguration).
    /// </summary>
    public void RemovePlayer(Guid userId)
    {
        var player = Players.FirstOrDefault(p => p.UserId == userId);

        if (player is not null)
        {
            Players.Remove(player);
        }
    }

    /// <summary>
    /// Hands hosting duties to another player already in the room — called
    /// when the current host leaves and players remain, so the room isn't
    /// left with a host who's no longer in it.
    /// </summary>
    public void TransferHost(Guid newHostUserId)
    {
        if (Players.All(p => p.UserId != newHostUserId))
            throw new InvalidOperationException("The new host must already be in the race.");

        HostUserId = newHostUserId;
    }

    public bool AllPlayersReady()
    {
        return Players.Count > 0 && Players.All(p => p.Status == PlayerRaceStatus.Ready);
    }

    public void Start()
    {
        if (Status != RaceStatus.Waiting)
            throw new InvalidOperationException("Race cannot be started.");

        Status = RaceStatus.Starting;
        StartedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Transitions Starting -> Running once the server-side countdown
    /// finishes (see RacesController.Start), and flips every player to
    /// Racing so their laps start counting.
    /// </summary>
    public void Begin()
    {
        if (Status != RaceStatus.Starting)
            throw new InvalidOperationException("Race cannot begin from its current state.");

        Status = RaceStatus.Running;

        foreach (var player in Players)
        {
            player.StartRacing();
        }
    }

    public void Finish()
    {
        Status = RaceStatus.Finished;
        FinishedAtUtc = DateTime.UtcNow;
    }
}