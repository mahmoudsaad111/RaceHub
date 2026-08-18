using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Tracks;

namespace RaceHub.Application.Features.Tracks.GetTracks;

/// <summary>Active track catalog for the "create race" track picker.</summary>
public record GetTracksQuery : IRequest<Result<IReadOnlyList<TrackDto>>>;
