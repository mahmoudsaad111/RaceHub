using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.StartRace;

public record StartRaceCommand(Guid RaceId, Guid RequestingUserId) : IRequest<Result<RaceDetailDto>>;
