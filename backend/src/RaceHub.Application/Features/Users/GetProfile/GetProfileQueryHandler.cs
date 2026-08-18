using MediatR;
using Microsoft.AspNetCore.Identity;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Users;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Users.GetProfile;

/// <summary>
/// Reads from PlayerStatistics/RaceHistoryEntry — the materialized,
/// asynchronously-maintained read models RankingWorker/StatisticsWorker
/// build off the race.finished event — rather than live-aggregating
/// RaceResult on every request. That's a deliberate eventual-consistency
/// tradeoff: immediately after a race finishes, this can lag the outbox
/// poll + queue delivery + consumer processing time (typically ~1-2s)
/// before reflecting it, in exchange for a cheap read here instead of a
/// GROUP BY over every RaceResult row on every profile view.
/// </summary>
public class GetProfileQueryHandler
    : IRequestHandler<GetProfileQuery, Result<ProfileDto>>
{
    // Flat XP-per-level curve: every level costs the same amount of XP.
    // Swap this out for a real curve later without touching the User
    // schema — Level/XpIntoLevel are derived on read, not stored.
    private const int XpPerLevel = 1000;
    private const int RecentRacesCount = 5;

    private readonly UserManager<User> _userManager;
    private readonly IPlayerStatisticsRepository _playerStatisticsRepository;
    private readonly IRaceHistoryRepository _raceHistoryRepository;
    private readonly ITrackRepository _trackRepository;

    public GetProfileQueryHandler(
        UserManager<User> userManager,
        IPlayerStatisticsRepository playerStatisticsRepository,
        IRaceHistoryRepository raceHistoryRepository,
        ITrackRepository trackRepository)
    {
        _userManager = userManager;
        _playerStatisticsRepository = playerStatisticsRepository;
        _raceHistoryRepository = raceHistoryRepository;
        _trackRepository = trackRepository;
    }

    public async Task<Result<ProfileDto>> Handle(
        GetProfileQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());

        if (user is null)
        {
            return Result<ProfileDto>.Failure("User not found.", "user_not_found");
        }

        // No PlayerStatistics row yet just means RankingWorker hasn't
        // processed this user's first race.finished event yet (or they
        // haven't raced at all) — not an error, just "all zeros."
        var stats = await _playerStatisticsRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var totalRaces = stats?.TotalRaces ?? 0;
        var wins = stats?.TotalWins ?? 0;
        var bestLapTime = stats?.BestTimeMs is int bestMs ? TimeSpan.FromMilliseconds(bestMs) : (TimeSpan?)null;

        var (recentEntries, _) = await _raceHistoryRepository.GetPagedByUserIdAsync(
            request.UserId, page: 1, pageSize: RecentRacesCount, cancellationToken);

        var trackNames = await _trackRepository.GetNamesByIdsAsync(
            recentEntries.Select(e => e.TrackId), cancellationToken);

        var recentRaces = recentEntries
            .Select(e => new RecentRaceDto(
                e.RaceId,
                trackNames.GetValueOrDefault(e.TrackId, "Unknown Track"),
                e.Position,
                TimeSpan.FromMilliseconds(e.FinishTimeMs),
                e.RecordedAtUtc))
            .ToList();

        var level = (user.Experience / XpPerLevel) + 1;
        var xpIntoLevel = user.Experience % XpPerLevel;

        var dto = new ProfileDto(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.Experience,
            user.Coins,
            level,
            xpIntoLevel,
            XpPerLevel,
            totalRaces,
            wins,
            bestLapTime,
            recentRaces,
            stats?.RatingPoints ?? 0);

        return Result<ProfileDto>.Success(dto);
    }
}
