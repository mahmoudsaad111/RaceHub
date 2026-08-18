using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Tracks;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Users.GetPersonalBests;

public class GetPersonalBestsQueryHandler
    : IRequestHandler<GetPersonalBestsQuery, Result<IReadOnlyList<PersonalBestDto>>>
{
    private readonly IRaceHistoryRepository _raceHistory;
    private readonly ITrackRepository _tracks;

    public GetPersonalBestsQueryHandler(
        IRaceHistoryRepository raceHistory,
        ITrackRepository tracks)
    {
        _raceHistory = raceHistory;
        _tracks = tracks;
    }

    public async Task<Result<IReadOnlyList<PersonalBestDto>>> Handle(
        GetPersonalBestsQuery request,
        CancellationToken cancellationToken)
    {
        var bestsByTrack = await _raceHistory.GetPersonalBestsByTrackAsync(request.UserId, cancellationToken);

        var names = await _tracks.GetNamesByIdsAsync(bestsByTrack.Keys, cancellationToken);

        var bests = bestsByTrack
            .Select(kv => new PersonalBestDto(
                kv.Key,
                names.GetValueOrDefault(kv.Key, "Unknown Track"),
                kv.Value))
            .OrderBy(b => b.TrackName)
            .ToList();

        return Result<IReadOnlyList<PersonalBestDto>>.Success(bests);
    }
}
