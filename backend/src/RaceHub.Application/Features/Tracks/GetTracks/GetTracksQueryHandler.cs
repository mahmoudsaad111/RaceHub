using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Tracks;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Tracks.GetTracks;

public class GetTracksQueryHandler
    : IRequestHandler<GetTracksQuery, Result<IReadOnlyList<TrackDto>>>
{
    private readonly ITrackRepository _trackRepository;

    public GetTracksQueryHandler(ITrackRepository trackRepository)
    {
        _trackRepository = trackRepository;
    }

    public async Task<Result<IReadOnlyList<TrackDto>>> Handle(
        GetTracksQuery request,
        CancellationToken cancellationToken)
    {
        var tracks = await _trackRepository.GetAllActiveAsync(cancellationToken);

        var dtos = tracks
            .Select(t => new TrackDto(
                t.Id,
                t.Name,
                t.Description,
                t.TotalLaps,
                t.Difficulty,
                // The catalog/picker list doesn't need full track geometry
                // (only the room's active track does, for canvas rendering)
                // — keep this query cheap and skip the Checkpoints include.
                Array.Empty<TrackCheckpointDto>()))
            .ToList();

        return Result<IReadOnlyList<TrackDto>>.Success(dtos);
    }
}
