namespace RaceHub.Application.DTOs.Leaderboards;

/// <summary>
/// RatingPoints is only meaningful on the global scope (it's
/// RankingWorker's Elo-style rating, maintained asynchronously off
/// race.finished events); the weekly/track scopes leave it at 0 and rank
/// by wins or best time instead.
/// </summary>
public record LeaderboardEntryDto(
    Guid UserId,
    string DisplayName,
    int Wins,
    int TotalRaces,
    TimeSpan? BestTime,
    int RatingPoints = 0);
