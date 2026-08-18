using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.ChangePlayerCar;

public record ChangePlayerCarCommand(Guid RaceId, Guid UserId, Guid CarId) : IRequest<Result<RaceDetailDto>>;
