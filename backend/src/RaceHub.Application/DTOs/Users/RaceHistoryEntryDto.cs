namespace RaceHub.Application.DTOs.Users;

public record RaceHistoryEntryDto(
    Guid RaceId,
    string TrackName,
    int Position,
    TimeSpan FinishTime,
    DateTime RecordedAtUtc);

public record PagedRaceHistoryDto(
    IReadOnlyList<RaceHistoryEntryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
