using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Leaderboards;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Leaderboards.GetLeaderboard;

public class GetLeaderboardQueryHandler
    : IRequestHandler<GetLeaderboardQuery, Result<IReadOnlyList<LeaderboardEntryDto>>>
{
    private readonly ILeaderboardRepository _leaderboardRepository;

    public GetLeaderboardQueryHandler(ILeaderboardRepository leaderboardRepository)
    {
        _leaderboardRepository = leaderboardRepository;
    }

    public async Task<Result<IReadOnlyList<LeaderboardEntryDto>>> Handle(
        GetLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        var scope = request.Scope.ToLowerInvariant();

        if (scope is not ("global" or "weekly" or "track"))
        {
            return Result<IReadOnlyList<LeaderboardEntryDto>>.Failure(
                "Scope must be one of: global, weekly, track.", "invalid_scope");
        }

        if (scope == "track" && request.TrackId is null)
        {
            return Result<IReadOnlyList<LeaderboardEntryDto>>.Failure(
                "trackId is required for the track scope.", "track_id_required");
        }

        var entries = await _leaderboardRepository.GetLeaderboardAsync(scope, request.TrackId, cancellationToken);

        return Result<IReadOnlyList<LeaderboardEntryDto>>.Success(entries);
    }
}
