using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Races.LeaveRace;

public record LeaveRaceCommand(Guid RaceId, Guid UserId) : IRequest<Result<LeaveRaceResult>>;
