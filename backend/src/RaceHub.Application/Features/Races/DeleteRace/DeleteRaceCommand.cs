using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Races.DeleteRace;

public record DeleteRaceCommand(Guid RaceId, Guid RequestingUserId) : IRequest<Result>;
