using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Races.GetOpenRaces;

public class GetOpenRacesQueryHandler
    : IRequestHandler<GetOpenRacesQuery, Result<IReadOnlyList<OpenRaceDto>>>
{
    private readonly IRaceRepository _raceRepository;

    public GetOpenRacesQueryHandler(IRaceRepository raceRepository)
    {
        _raceRepository = raceRepository;
    }

    public async Task<Result<IReadOnlyList<OpenRaceDto>>> Handle(
        GetOpenRacesQuery request,
        CancellationToken cancellationToken)
    {
        var races = await _raceRepository.GetOpenAsync(cancellationToken);

        var dtos = races
            .Select(r => new OpenRaceDto(
                r.Id,
                r.Track.Name,
                r.Players.Count,
                r.MaxPlayers,
                r.TotalLaps))
            .ToList();

        return Result<IReadOnlyList<OpenRaceDto>>.Success(dtos);
    }
}
