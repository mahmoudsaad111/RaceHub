using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RaceHub.API.Hubs;
using RaceHubHub = RaceHub.API.Hubs.RaceHub;
using RaceHub.Application.Features.Races.BeginRace;
using RaceHub.Application.Features.Races.AcceptInvite;
using RaceHub.Application.Features.Races.ChangePlayerCar;
using RaceHub.Application.Features.Races.CreateRace;
using RaceHub.Application.Features.Races.DeleteRace;
using RaceHub.Application.Features.Races.GetOpenRaces;
using RaceHub.Application.Features.Races.GetRaceById;
using RaceHub.Application.Features.Races.GetRaceResults;
using RaceHub.Application.Features.Races.JoinRace;
using RaceHub.Application.Features.Races.LeaveRace;
using RaceHub.Application.Features.Races.SetPlayerReady;
using RaceHub.Application.Features.Races.StartRace;

namespace RaceHub.API.Controllers;

[Route("api/races")]
[Authorize]
public class RacesController : ApiControllerBase
{
    private const int CountdownStartSeconds = 3;
    private static readonly TimeSpan CountdownTick = TimeSpan.FromSeconds(1);

    private readonly ISender _sender;
    private readonly IHubContext<RaceHubHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RacesController> _logger;

    public RacesController(
        ISender sender,
        IHubContext<RaceHubHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<RacesController> logger)
    {
        _sender = sender;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Open rooms for the lobby's "Open Rooms" list.</summary>
    [HttpGet]
    public async Task<IActionResult> GetOpenRaces(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetOpenRacesQuery(), cancellationToken);

        return HandleResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetRaceByIdQuery(id), cancellationToken);

        return HandleResult(result);
    }

    [HttpGet("{id:guid}/results")]
    public async Task<IActionResult> GetResults(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetRaceResultsQuery(id), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Body: { "trackId": "...", "carId": "...", "maxPlayers": 8 }</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRaceBody body, CancellationToken cancellationToken)
    {
        var command = new CreateRaceCommand(CurrentUserId, body.TrackId, body.CarId, body.MaxPlayers);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, "Room created.");
    }

    /// <summary>Body: { "carId": "..." }</summary>
    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id, [FromBody] JoinRaceBody body, CancellationToken cancellationToken)
    {
        var command = new JoinRaceCommand(id, CurrentUserId, body.CarId);

        var result = await _sender.Send(command, cancellationToken);

        if (result.Succeeded)
        {
            await _hubContext.Clients
                .Group(RaceHubHub.GroupName(id))
                .SendAsync("PlayerJoined", result.Value, cancellationToken);
        }

        return HandleResult(result, "Joined room.");
    }

    /// <summary>
    /// Used by invite acceptance. The server selects a car and adds the
    /// player to the race before the client navigates to the room screen.
    /// </summary>
    [HttpPost("{id:guid}/accept-invite")]
    public async Task<IActionResult> AcceptInvite(Guid id, CancellationToken cancellationToken)
    {
        var command = new AcceptRaceInviteCommand(id, CurrentUserId);

        var result = await _sender.Send(command, cancellationToken);

        if (result.Succeeded)
        {
            await _hubContext.Clients
                .Group(RaceHubHub.GroupName(id))
                .SendAsync("PlayerJoined", result.Value, cancellationToken);
        }

        return HandleResult(result, "Invite accepted.");
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        var command = new LeaveRaceCommand(id, CurrentUserId);

        var result = await _sender.Send(command, cancellationToken);

        if (result.Succeeded)
        {
            if (result.Value!.RoomClosed)
            {
                await _hubContext.Clients
                    .Group(RaceHubHub.GroupName(id))
                    .SendAsync("RoomClosed", new { raceId = id }, cancellationToken);
            }
            else
            {
                await _hubContext.Clients
                    .Group(RaceHubHub.GroupName(id))
                    .SendAsync("PlayerLeft", result.Value.RaceDetail, cancellationToken);
            }
        }

        return HandleResult(result, "Left room.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteRaceCommand(id, CurrentUserId);

        var result = await _sender.Send(command, cancellationToken);

        if (result.Succeeded)
        {
            await _hubContext.Clients
                .Group(RaceHubHub.GroupName(id))
                .SendAsync("RoomDeleted", new { raceId = id }, cancellationToken);
        }

        return HandleResult(result, "Room deleted.");
    }

    [HttpPost("{id:guid}/ready")]
    public async Task<IActionResult> Ready(Guid id, CancellationToken cancellationToken)
    {
        var command = new SetPlayerReadyCommand(id, CurrentUserId);

        var result = await _sender.Send(command, cancellationToken);

        if (result.Succeeded)
        {
            await _hubContext.Clients
                .Group(RaceHubHub.GroupName(id))
                .SendAsync("PlayerReady", result.Value, cancellationToken);
        }

        return HandleResult(result, "Readiness updated.");
    }

    /// <summary>Body: { "carId": "..." }. Only available to unready players in a waiting room.</summary>
    [HttpPut("{id:guid}/car")]
    public async Task<IActionResult> ChangeCar(Guid id, [FromBody] ChangeCarBody body, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ChangePlayerCarCommand(id, CurrentUserId, body.CarId), cancellationToken);

        if (result.Succeeded)
        {
            await _hubContext.Clients
                .Group(RaceHubHub.GroupName(id))
                .SendAsync("PlayerReady", result.Value, cancellationToken);
        }

        return HandleResult(result, "Car changed.");
    }

    /// <summary>Host-only. Fails with "not_all_ready" until every player has readied up.</summary>
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        var command = new StartRaceCommand(id, CurrentUserId);

        var result = await _sender.Send(command, cancellationToken);

        if (result.Succeeded)
        {
            await _hubContext.Clients
                .Group(RaceHubHub.GroupName(id))
                .SendAsync("RaceStarted", result.Value, cancellationToken);

            // Not awaited: the HTTP response returns immediately after
            // "RaceStarted" goes out, while the 3-2-1-GO sequence plays out
            // over the following few seconds purely over SignalR. Runs in
            // its own DI scope (via IServiceScopeFactory) because this
            // controller's own scoped services are disposed as soon as the
            // request completes.
            _ = RunCountdownAsync(id);
        }

        return HandleResult(result, "Race started.");
    }

    private async Task RunCountdownAsync(Guid raceId)
    {
        var group = _hubContext.Clients.Group(Hubs.RaceHub.GroupName(raceId));

        try
        {
            for (var seconds = CountdownStartSeconds; seconds >= 1; seconds--)
            {
                await Task.Delay(CountdownTick);
                await group.SendAsync("RaceCountdown", new { raceId, seconds });
            }

            await Task.Delay(CountdownTick);

            using var scope = _scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var beginResult = await sender.Send(new BeginRaceCommand(raceId));

            if (beginResult.Succeeded)
            {
                await group.SendAsync("RaceBegin", new { raceId, serverStartUtc = DateTime.UtcNow });
            }
            else
            {
                _logger.LogWarning(
                    "BeginRaceCommand failed for race {RaceId}: {Error}",
                    raceId,
                    beginResult.Error);
            }
        }
        catch (Exception ex)
        {
            // Background task — nothing is awaiting this, so an unhandled
            // exception here would otherwise vanish silently instead of
            // surfacing anywhere.
            _logger.LogError(ex, "Countdown for race {RaceId} failed.", raceId);
        }
    }

    private Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(claim, out var id)
                ? id
                : throw new UnauthorizedAccessException("No valid userId claim on the access token.");
        }
    }
}

public record CreateRaceBody(Guid TrackId, Guid CarId, int MaxPlayers);

public record JoinRaceBody(Guid CarId);

public record ChangeCarBody(Guid CarId);
