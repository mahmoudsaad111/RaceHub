using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Tracks;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Tracks.GetTrackById;

public class GetTrackByIdQueryHandler
    : IRequestHandler<GetTrackByIdQuery, Result<TrackDto>>
{
    private readonly ITrackRepository _trackRepository;

    public GetTrackByIdQueryHandler(ITrackRepository trackRepository)
    {
        _trackRepository = trackRepository;
    }

    public async Task<Result<TrackDto>> Handle(
        GetTrackByIdQuery request,
        CancellationToken cancellationToken)
    {
        var track = await _trackRepository.GetByIdAsync(request.TrackId, cancellationToken);

        if (track is null)
        {
            return Result<TrackDto>.Failure("Track not found.", "track_not_found");
        }

        var dto = new TrackDto(
            track.Id,
            track.Name,
            track.Description,
            track.TotalLaps,
            track.Difficulty,
            track.Checkpoints
                .OrderBy(c => c.Sequence)
                .Select(c => new TrackCheckpointDto(c.Sequence, c.PositionX, c.PositionY, c.Width))
                .ToList());

        return Result<TrackDto>.Success(dto);
    }
}
