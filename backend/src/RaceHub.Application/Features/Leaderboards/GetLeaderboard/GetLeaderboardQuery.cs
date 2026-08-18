using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Leaderboards;

namespace RaceHub.Application.Features.Leaderboards.GetLeaderboard;

/// <summary>
/// Scope: "global" (default), "weekly", or "track" (requires TrackId).
/// </summary>
public record GetLeaderboardQuery(string Scope = "global", Guid? TrackId = null)
    : IRequest<Result<IReadOnlyList<LeaderboardEntryDto>>>;
