using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Tracks.GetTrackById;

public record GetTrackByIdQuery(Guid TrackId)
    : IRequest<Result<RaceHub.Application.DTOs.Tracks.TrackDto>>;
