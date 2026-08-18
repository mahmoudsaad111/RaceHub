using RaceHub.Domain.Enums;

namespace RaceHub.Domain.Entities;

public class RacePlayer : BaseEntity
{
    public Guid RaceId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid CarId { get; private set; }

    public PlayerRaceStatus Status { get; private set; }

    public int CurrentLap { get; private set; }

    public int CurrentCheckpoint { get; private set; }

    public int? FinishingPosition { get; private set; }

    public TimeSpan? BestLapTime { get; private set; }

    public TimeSpan? TotalRaceTime { get; private set; }

    public DateTime? FinishedAtUtc { get; private set; }

    public Race Race { get; private set; } = null!;

    public User User { get; private set; } = null!;

    public Car Car { get; private set; } = null!;

    public ICollection<Lap> Laps { get; private set; }
        = new List<Lap>();

    private RacePlayer() { }

    public RacePlayer(
        Guid raceId,
        Guid userId,
        Guid carId)
    {
        RaceId = raceId;
        UserId = userId;
        CarId = carId;

        Status = PlayerRaceStatus.Waiting;
        CurrentLap = 0;
        CurrentCheckpoint = 0;
    }

    public void Ready()
    {
        Status = PlayerRaceStatus.Ready;
    }

    public void ToggleReady()
    {
        if (Status == PlayerRaceStatus.Ready)
        {
            Status = PlayerRaceStatus.Waiting;
            return;
        }

        if (Status == PlayerRaceStatus.Waiting)
        {
            Status = PlayerRaceStatus.Ready;
            return;
        }

        throw new InvalidOperationException("Readiness can only be changed before the race starts.");
    }

    public void ChangeCar(Guid carId)
    {
        if (Status != PlayerRaceStatus.Waiting)
            throw new InvalidOperationException("Unready before changing your car.");

        CarId = carId;
    }

    /// <summary>Called on every player when Race.Begin() fires, after the countdown.</summary>
    public void StartRacing()
    {
        Status = PlayerRaceStatus.Racing;
    }

    /// <summary>
    /// Records a completed lap: bumps CurrentLap, keeps BestLapTime updated,
    /// and appends a Lap row for history/replay. Called from
    /// RecordLapCommandHandler in response to the client's
    /// ReportLapCompleted hub call.
    /// </summary>
    public Lap CompleteLap(int lapNumber, TimeSpan lapTime)
    {
        CurrentLap = lapNumber;

        if (BestLapTime is null || lapTime < BestLapTime)
        {
            BestLapTime = lapTime;
        }

        var lap = new Lap(Id, lapNumber, lapTime);
        Laps.Add(lap);
        return lap;
    }

    public void Finish(int position, TimeSpan totalRaceTime)
    {
        Status = PlayerRaceStatus.Finished;
        FinishingPosition = position;
        TotalRaceTime = totalRaceTime;
        FinishedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks a player as no longer actively racing without deleting their
    /// RacePlayer row (which would cascade-delete their Lap history) —
    /// called when their SignalR connection drops while the race is
    /// Starting/Running. Treated the same as Finished for "has everyone
    /// wrapped up" checks (see FinishPlayerCommandHandler) so one
    /// abandoned player can't leave a race stuck at Running forever for
    /// everyone else, including players who already finished and moved on.
    /// </summary>
    public void MarkDisconnected()
    {
        if (Status == PlayerRaceStatus.Finished)
        {
            return; // already done — don't overwrite a real finish result
        }

        Status = PlayerRaceStatus.Disconnected;
    }
}
