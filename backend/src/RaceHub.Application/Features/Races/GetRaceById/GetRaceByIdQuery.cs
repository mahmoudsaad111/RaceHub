using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.GetRaceById;

public record GetRaceByIdQuery(Guid RaceId) : IRequest<Result<RaceDetailDto>>;
