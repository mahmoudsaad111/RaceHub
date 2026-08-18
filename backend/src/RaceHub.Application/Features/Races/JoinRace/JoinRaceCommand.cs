using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.JoinRace;

public record JoinRaceCommand(Guid RaceId, Guid UserId, Guid CarId) : IRequest<Result<RaceDetailDto>>;
