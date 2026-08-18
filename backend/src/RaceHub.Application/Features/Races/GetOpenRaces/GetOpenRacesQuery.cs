using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.GetOpenRaces;

public record GetOpenRacesQuery : IRequest<Result<IReadOnlyList<OpenRaceDto>>>;
