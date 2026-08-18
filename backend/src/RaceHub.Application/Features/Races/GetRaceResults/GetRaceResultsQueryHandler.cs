using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Races.GetRaceResults;

public class GetRaceResultsQueryHandler
    : IRequestHandler<GetRaceResultsQuery, Result<RaceFinishedDto>>
{
    private readonly IRaceResultRepository _raceResultRepository;

    public GetRaceResultsQueryHandler(IRaceResultRepository raceResultRepository)
    {
        _raceResultRepository = raceResultRepository;
    }

    public async Task<Result<RaceFinishedDto>> Handle(
        GetRaceResultsQuery request,
        CancellationToken cancellationToken)
    {
        var results = await _raceResultRepository.GetByRaceIdAsync(request.RaceId, cancellationToken);

        if (results.Count == 0)
        {
            return Result<RaceFinishedDto>.Failure("No results found for this race.", "results_not_found");
        }

        var dtos = results
            .Select(r => new RaceResultRowDto(
                r.FinishingPosition,
                r.UserId,
                r.User.DisplayName,
                (int)r.TotalRaceTime.TotalMilliseconds,
                r.BestLapTime is null ? null : (int)r.BestLapTime.Value.TotalMilliseconds,
                r.ExperienceEarned,
                r.CoinsEarned))
            .ToList();

        return Result<RaceFinishedDto>.Success(new RaceFinishedDto(request.RaceId, dtos));
    }
}
