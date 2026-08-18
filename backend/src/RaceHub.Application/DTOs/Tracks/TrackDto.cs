namespace RaceHub.Application.DTOs.Tracks;

/// <summary>
/// One gate along the track path, in canvas coordinate space (see
/// Program.cs seeding — currently a 1600x900 arena). Width is the gate's
/// tolerance for client-side crossing detection: a car is considered to
/// have passed the checkpoint when it comes within Width/2 of
/// (PositionX, PositionY). Sequence 0 is the start/finish line.
/// </summary>
public record TrackCheckpointDto(
    int Sequence,
    decimal PositionX,
    decimal PositionY,
    decimal Width);

public record TrackDto(
    Guid Id,
    string Name,
    string Description,
    int TotalLaps,
    int Difficulty,
    IReadOnlyList<TrackCheckpointDto> Checkpoints);
