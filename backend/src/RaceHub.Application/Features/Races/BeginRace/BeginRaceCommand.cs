using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Races.BeginRace;

/// <summary>
/// Fired server-side once the post-"RaceStarted" countdown finishes (see
/// RacesController.RunCountdownAsync) — never called directly by a client.
/// </summary>
public record BeginRaceCommand(Guid RaceId) : IRequest<Result>;
