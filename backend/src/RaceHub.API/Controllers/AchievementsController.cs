using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceHub.Application.Features.Achievements.GetAchievements;

namespace RaceHub.API.Controllers;

[Route("api/achievements")]
[Authorize]
public class AchievementsController : ApiControllerBase
{
    private readonly ISender _sender;

    public AchievementsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Full achievement catalog with the current user's unlock status —
    /// locked badges included (greyed out in the UI) so players can see
    /// what to chase. Unlocks themselves happen in
    /// RaceHub.AchievementsWorker off the race.finished event.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var claim = User.FindFirst("userId")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(claim, out var userId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new GetAchievementsQuery(userId), cancellationToken);

        return HandleResult(result);
    }
}
