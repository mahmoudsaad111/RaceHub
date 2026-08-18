namespace RaceHub.Application.DTOs.Tracks;

/// <summary>
/// This user's fastest finish time on a track — powers the
/// "your PB: 1:23.456" hint on the track picker. Sourced from
/// RaceHistoryEntry (StatisticsWorker's read model), not RaceResult.
/// </summary>
public record PersonalBestDto(
    Guid TrackId,
    string TrackName,
    int BestTimeMs);
