using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceHub.Application.Features.Leaderboards.GetLeaderboard;

namespace RaceHub.API.Controllers;

[Route("api/leaderboards")]
[Authorize]
public class LeaderboardsController : ApiControllerBase
{
    private readonly ISender _sender;

    public LeaderboardsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Leaderboard entries. Accepts ?scope=global (default), ?scope=weekly,
    /// or ?scope=track&amp;trackId=... (fastest on a single track).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? scope,
        [FromQuery] Guid? trackId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetLeaderboardQuery(scope ?? "global", trackId),
            cancellationToken);

        return HandleResult(result);
    }
}
