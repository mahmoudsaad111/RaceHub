namespace RaceHub.Application.DTOs.Races;

/// <summary>Row in the lobby's open-room list.</summary>
public record OpenRaceDto(
    Guid Id,
    string TrackName,
    int CurrentPlayers,
    int MaxPlayers,
    int TotalLaps);

public record RacePlayerDto(
    Guid UserId,
    string DisplayName,
    Guid CarId,
    string CarName,
    string Status,
    bool IsHost);

/// <summary>Full room state — used by the room screen and the SignalR broadcasts.</summary>
public record RaceDetailDto(
    Guid Id,
    Guid TrackId,
    string TrackName,
    int TotalLaps,
    Guid HostUserId,
    string Status,
    int MaxPlayers,
    IReadOnlyList<RacePlayerDto> Players);

// ---- In-race real-time payloads (SignalR broadcasts from RaceHub) ----

/// <summary>
/// Pure relay, not persisted — one client's ReportProgress call fanned out
/// to everyone else in the race group so opponents' cars move on the
/// track view. Progress is 0-1 (fraction of the current lap completed).
/// </summary>
public record PlayerProgressDto(Guid RaceId, Guid UserId, int Lap, int Checkpoint, double Progress);

/// <summary>Broadcast whenever any player crosses the finish line for a lap.</summary>
public record PlayerLapDto(Guid RaceId, Guid UserId, int LapNumber, int LapTimeMs, int BestLapTimeMs);

/// <summary>Broadcast the moment a single player finishes the race.</summary>
public record PlayerFinishedDto(Guid RaceId, Guid UserId, int Position, int TotalTimeMs, int? BestLapTimeMs);

/// <summary>One row of the final standings shown on the Results screen.</summary>
public record RaceResultRowDto(
    int Position,
    Guid UserId,
    string DisplayName,
    int TotalTimeMs,
    int? BestLapTimeMs,
    int ExperienceEarned,
    int CoinsEarned);

/// <summary>Broadcast once every player has finished — the whole race is over.</summary>
public record RaceFinishedDto(Guid RaceId, IReadOnlyList<RaceResultRowDto> Results);
