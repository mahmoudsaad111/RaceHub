using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceHub.Application.Features.Users.GetPersonalBests;
using RaceHub.Application.Features.Users.GetProfile;
using RaceHub.Application.Features.Users.GetRaceHistory;

namespace RaceHub.API.Controllers;

[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly ISender _sender;

    public UsersController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Profile screen data for the currently authenticated user — reads the
    /// userId claim baked into the access token by TokenService, same
    /// convention as GET /api/auth/me.
    /// </summary>
    [HttpGet("me/profile")]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _sender.Send(new GetProfileQuery(userId.Value), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Paginated race history for the currently authenticated user, newest first.</summary>
    [HttpGet("me/race-history")]
    public async Task<IActionResult> GetMyRaceHistory(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var query = new GetRaceHistoryQuery(
            userId.Value,
            page <= 0 ? 1 : page,
            pageSize <= 0 ? 20 : pageSize);

        var result = await _sender.Send(query, cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// The current user's best finish time on each track they've raced —
    /// powers the "your PB" hint on the track picker. Read from
    /// RaceHistoryEntry (StatisticsWorker's read model), not RaceResult.
    /// </summary>
    [HttpGet("me/personal-bests")]
    public async Task<IActionResult> GetMyPersonalBests(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _sender.Send(new GetPersonalBestsQuery(userId.Value), cancellationToken);

        return HandleResult(result);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("userId")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
