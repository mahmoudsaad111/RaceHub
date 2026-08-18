using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Users;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Users.GetRaceHistory;

public class GetRaceHistoryQueryHandler
    : IRequestHandler<GetRaceHistoryQuery, Result<PagedRaceHistoryDto>>
{
    private const int MaxPageSize = 50;

    private readonly IRaceHistoryRepository _raceHistoryRepository;
    private readonly ITrackRepository _trackRepository;

    public GetRaceHistoryQueryHandler(
        IRaceHistoryRepository raceHistoryRepository,
        ITrackRepository trackRepository)
    {
        _raceHistoryRepository = raceHistoryRepository;
        _trackRepository = trackRepository;
    }

    public async Task<Result<PagedRaceHistoryDto>> Handle(
        GetRaceHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var (entries, totalCount) = await _raceHistoryRepository.GetPagedByUserIdAsync(
            request.UserId, page, pageSize, cancellationToken);

        var trackNames = await _trackRepository.GetNamesByIdsAsync(
            entries.Select(e => e.TrackId), cancellationToken);

        var items = entries
            .Select(e => new RaceHistoryEntryDto(
                e.RaceId,
                trackNames.GetValueOrDefault(e.TrackId, "Unknown Track"),
                e.Position,
                TimeSpan.FromMilliseconds(e.FinishTimeMs),
                e.RecordedAtUtc))
            .ToList();

        var dto = new PagedRaceHistoryDto(items, page, pageSize, totalCount);

        return Result<PagedRaceHistoryDto>.Success(dto);
    }
}
