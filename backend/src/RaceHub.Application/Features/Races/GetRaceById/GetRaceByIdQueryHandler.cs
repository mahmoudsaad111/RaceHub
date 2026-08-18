using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Features.Races.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Races.GetRaceById;

public class GetRaceByIdQueryHandler : IRequestHandler<GetRaceByIdQuery, Result<RaceDetailDto>>
{
    private readonly IRaceRepository _raceRepository;

    public GetRaceByIdQueryHandler(IRaceRepository raceRepository)
    {
        _raceRepository = raceRepository;
    }

    public async Task<Result<RaceDetailDto>> Handle(GetRaceByIdQuery request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetSnapshotByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result<RaceDetailDto>.Failure("Race not found.", "race_not_found");
        }

        return Result<RaceDetailDto>.Success(RaceMapper.ToDetailDto(race));
    }
}
