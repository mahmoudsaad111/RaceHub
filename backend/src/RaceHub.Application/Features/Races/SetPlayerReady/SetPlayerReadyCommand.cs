using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.SetPlayerReady;

public record SetPlayerReadyCommand(Guid RaceId, Guid UserId) : IRequest<Result<RaceDetailDto>>;
