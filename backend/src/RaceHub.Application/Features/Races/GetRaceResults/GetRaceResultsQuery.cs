using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Races.GetRaceResults;

public record GetRaceResultsQuery(Guid RaceId)
    : IRequest<Result<RaceHub.Application.DTOs.Races.RaceFinishedDto>>;
