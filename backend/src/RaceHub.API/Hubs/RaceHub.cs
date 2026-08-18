using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RaceHub.Application.Features.Races.Common;
using RaceHub.Application.Features.Races.FinishRace;
using RaceHub.Application.Features.Races.RecordLap;
using RaceHub.Application.Interfaces;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Enums;

namespace RaceHub.API.Hubs;

/// <summary>
/// Four responsibilities, all real-time:
///
/// 1. Room membership — clients call JoinRaceGroup/LeaveRaceGroup after a
///    successful REST join/leave so they land in the "race-{id}" group and
///    receive RacesController's broadcasts (PlayerJoined, PlayerLeft,
///    PlayerReady, RaceStarted, RaceCountdown, RaceBegin, RoomClosed).
/// 2. In-race telemetry — once a race is Running, connected clients call
///    ReportProgress/ReportLapCompleted/ReportFinished directly on this
///    hub (not via REST) since this data is latency-sensitive and streamed
///    continuously rather than being a one-off state change. Lap/finish
///    calls still go through MediatR commands so they're validated and
///    persisted exactly like everything else; ReportProgress is a pure
///    relay with no persistence, since it's cosmetic (opponent car
///    positions) rather than race-authoritative.
/// 3. Friend presence — on connect/disconnect, notifies the user's
///    accepted friends via Clients.User(...). This relies on
///    RaceHubUserIdProvider (registered in Program.cs) to resolve
///    Context.UserIdentifier from the "userId" claim — SignalR's default
///    ClaimTypes.NameIdentifier-based provider doesn't work here since
///    TokenService never sets that claim.
/// 4. Race invites — a player already in a room can invite a specific
///    friend to join it (InviteFriendToRace); the friend can decline
///    (DeclineRaceInvite) or simply accept by calling the normal REST
///    join endpoint, which re-validates the room is still joinable at
///    that moment rather than trusting the invite-time snapshot.
///
/// Presence is tracked by IPresenceTracker (in-memory), which is fine for
/// a single API instance. If RaceHub.API is ever scaled out to multiple
/// instances, this needs a Redis-backed implementation (see
/// RaceHub.Infrastructure/Redis) both for SignalR's own scale-out story
/// (AddStackExchangeRedis) and so presence counts are shared across
/// instances instead of siloed per-pod.
/// </summary>
[Authorize]
public class RaceHub : Hub
{
    private readonly IFriendshipRepository _friendshipRepository;
    private readonly IRaceRepository _raceRepository;
    private readonly IPresenceTracker _presenceTracker;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _sender;

    public RaceHub(
        IFriendshipRepository friendshipRepository,
        IRaceRepository raceRepository,
        IPresenceTracker presenceTracker,
        IUnitOfWork unitOfWork,
        ISender sender)
    {
        _friendshipRepository = friendshipRepository;
        _raceRepository = raceRepository;
        _presenceTracker = presenceTracker;
        _unitOfWork = unitOfWork;
        _sender = sender;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var displayName = Context.User?.FindFirst("displayName")?.Value ?? "Racer";

        if (_presenceTracker.AddConnection(userId))
        {
            await NotifyFriends(userId, "FriendOnline", new { userId, displayName });
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();

        if (_presenceTracker.RemoveConnection(userId))
        {
            await NotifyFriends(userId, "FriendOffline", new { userId });
            await LeaveWaitingRoomsAsync(userId);
            await MarkDisconnectedInActiveRacesAsync(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Called by the client right after a successful POST /api/races/{id}/join.</summary>
    public Task JoinRaceGroup(Guid raceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(raceId));

    /// <summary>Called by the client right after a successful POST /api/races/{id}/leave.</summary>
    public Task LeaveRaceGroup(Guid raceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(raceId));

    /// <summary>
    /// Streamed ~5-10x/sec by each racer's own client while the race is
    /// Running, so opponents' cars move smoothly on the track view.
    /// Fire-and-forget by design: not persisted, not race-authoritative,
    /// safe to drop a few frames if the connection hiccups.
    /// </summary>
    public Task ReportProgress(Guid raceId, int lap, int checkpoint, double progress)
    {
        var userId = GetUserId();

        return Clients.OthersInGroup(GroupName(raceId))
            .SendAsync("PlayerProgress", new { raceId, userId, lap, checkpoint, progress });
    }

    /// <summary>Called once per lap, when the client detects it crossed the start/finish line.</summary>
    public async Task ReportLapCompleted(Guid raceId, int lapNumber, int lapTimeMs)
    {
        var userId = GetUserId();

        var result = await SendSafely(() => _sender.Send(new RecordLapCommand(raceId, userId, lapNumber, lapTimeMs)));

        if (result is null)
        {
            return;
        }

        if (result.Succeeded)
        {
            await Clients.Group(GroupName(raceId)).SendAsync("PlayerLapCompleted", result.Value);
        }
        else
        {
            await Clients.Caller.SendAsync("RaceError", new { error = result.Error, code = result.ErrorCode });
        }
    }

    /// <summary>Called once, when the client detects it crossed the final lap's finish line.</summary>
    public async Task ReportFinished(Guid raceId, int totalTimeMs)
    {
        var userId = GetUserId();

        var result = await SendSafely(() => _sender.Send(new FinishPlayerCommand(raceId, userId, totalTimeMs)));

        if (result is null)
        {
            return;
        }

        if (!result.Succeeded)
        {
            await Clients.Caller.SendAsync("RaceError", new { error = result.Error, code = result.ErrorCode });
            return;
        }

        await Clients.Group(GroupName(raceId)).SendAsync("PlayerFinished", result.Value!.PlayerFinished);

        if (result.Value.RaceFinished)
        {
            await Clients.Group(GroupName(raceId)).SendAsync("RaceFinished", result.Value.FinalResults);
        }
    }

    public async Task SendRaceMessage(Guid raceId, string content)
    {
        var userId = GetUserId();

        await Clients.Group(GroupName(raceId)).SendAsync("RaceChatMessage", new
        {
            senderId = userId,
            content,
            sentAtUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Invites a specific friend to the caller's current room. Real-time
    /// only, nothing persisted — same pattern as chat/progress. The friend
    /// accepts simply by calling POST /api/races/{id}/join themselves
    /// (which re-checks room/full/started at that moment), or declines via
    /// DeclineRaceInvite below. Guards against inviting a non-friend or
    /// inviting into a room that's already full/started/gone, but those
    /// guards are a courtesy, not a source of truth — the real check
    /// happens at join time regardless.
    /// </summary>
    public async Task InviteFriendToRace(Guid raceId, Guid friendUserId)
    {
        var userId = GetUserId();

        var friendships = await _friendshipRepository.GetAcceptedForUserAsync(userId);
        var isFriend = friendships.Any(f => f.RequesterId == friendUserId || f.AddresseeId == friendUserId);

        if (!isFriend)
        {
            await Clients.Caller.SendAsync("RaceError", new { error = "You can only invite friends.", code = "not_a_friend" });
            return;
        }

        var race = await _raceRepository.GetByIdAsync(raceId);

        if (race is null || race.Status != RaceStatus.Waiting)
        {
            await Clients.Caller.SendAsync("RaceError", new { error = "This race can no longer be joined.", code = "race_not_joinable" });
            return;
        }

        if (race.Players.Count >= race.MaxPlayers)
        {
            await Clients.Caller.SendAsync("RaceError", new { error = "This race is full.", code = "race_full" });
            return;
        }

        var displayName = Context.User?.FindFirst("displayName")?.Value ?? "A friend";

        await Clients.User(friendUserId.ToString()).SendAsync("RaceInviteReceived", new
        {
            raceId,
            trackName = race.Track.Name,
            fromUserId = userId,
            fromDisplayName = displayName,
            currentPlayers = race.Players.Count,
            maxPlayers = race.MaxPlayers
        });
    }

    /// <summary>Lets the invitee tell the host they're not coming, so the host's UI can drop the "pending" state instead of guessing from silence.</summary>
    public async Task DeclineRaceInvite(Guid raceId, Guid hostUserId)
    {
        var userId = GetUserId();
        var displayName = Context.User?.FindFirst("displayName")?.Value ?? "A friend";

        await Clients.User(hostUserId.ToString()).SendAsync("RaceInviteDeclined", new
        {
            raceId,
            byUserId = userId,
            byDisplayName = displayName
        });
    }

    public static string GroupName(Guid raceId) => $"race-{raceId}";

    /// <summary>
    /// If the user's last connection just dropped while they were sitting
    /// in one or more rooms that haven't started yet (Status == Waiting),
    /// removes them from each and broadcasts the update — mirrors exactly
    /// what RacesController.Leave does for an explicit "Leave Room" click.
    ///
    /// Without this, closing the tab / losing connection / a crash left
    /// the RacePlayer row behind forever, since nothing else ever cleaned
    /// it up. That silently broke room state in a few compounding ways:
    /// the room looked occupied by someone who wasn't there, friends'
    /// "join their room" button (GetFriendsQueryHandler) kept pointing at
    /// a room the person had actually left, and worst of all
    /// AllPlayersReady() could never return true again — a disconnected
    /// phantom player can never click Ready, permanently blocking Start
    /// Race for everyone still actually in the room.
    ///
    /// Deliberately scoped to Waiting only: once a race is
    /// Starting/Running, a dropped connection might just be a brief
    /// network blip mid-race, and yanking the player's RacePlayer row out
    /// from under an in-progress race would corrupt lap/finish tracking
    /// for no good reason — that's a separate reconnect/grace-period
    /// problem, not this one.
    /// </summary>
    private async Task LeaveWaitingRoomsAsync(Guid userId)
    {
        var races = await _raceRepository.GetWaitingRacesForUserAsync(userId);

        foreach (var race in races)
        {
            var raceId = race.Id;
            var wasHost = race.HostUserId == userId;

            race.RemovePlayer(userId);

            if (race.Players.Count == 0)
            {
                _raceRepository.Remove(race);
                await _unitOfWork.SaveChangesAsync();

                await Clients.Group(GroupName(raceId)).SendAsync("RoomClosed", new { raceId });
                continue;
            }

            if (wasHost)
            {
                race.TransferHost(race.Players.First().UserId);
            }

            await _unitOfWork.SaveChangesAsync();

            var hydrated = await _raceRepository.GetSnapshotByIdAsync(raceId);

            if (hydrated is not null)
            {
                await Clients.Group(GroupName(raceId)).SendAsync("PlayerLeft", RaceMapper.ToDetailDto(hydrated));
            }
        }
    }

    /// <summary>
    /// If the user's last connection just dropped while a race they're in
    /// is Starting/Running, marks their RacePlayer Disconnected instead of
    /// removing it entirely (removing would cascade-delete their Lap
    /// history via the RacePlayerId FK). This is what makes
    /// PlayerRaceStatus.Disconnected mean something — it existed in the
    /// enum but nothing ever set it. Without this, an abandoned mid-race
    /// player sat at Status.Racing forever, so
    /// FinishPlayerCommandHandler's "has everyone finished" check
    /// (Players.All(p =&gt; p.Status == Finished)) could never become true
    /// again: the race stayed stuck at Running permanently, and kept
    /// showing up as "currently racing" on every remaining player's
    /// friends list forever — including players who'd already finished
    /// themselves and moved on — since GetActiveRacesForUsersAsync only
    /// excludes races that are actually RaceStatus.Finished.
    ///
    /// If marking this player Disconnected means every remaining player is
    /// now either Finished or Disconnected, the race is wrapped up here
    /// too (mirroring FinishPlayerCommandHandler's own check), rather than
    /// leaving a fully-abandoned race stuck at Running with nobody left to
    /// trigger the finish check from the other side.
    /// </summary>
    private async Task MarkDisconnectedInActiveRacesAsync(Guid userId)
    {
        var races = await _raceRepository.GetInProgressRacesForUserAsync(userId);

        foreach (var race in races)
        {
            var player = race.Players.FirstOrDefault(p => p.UserId == userId);

            if (player is null)
            {
                continue;
            }

            player.MarkDisconnected();

            if (race.Players.All(p => p.Status == PlayerRaceStatus.Finished || p.Status == PlayerRaceStatus.Disconnected))
            {
                race.Finish();
            }

            await _unitOfWork.SaveChangesAsync();
        }
    }

    /// <summary>
    /// FluentValidation throws ValidationException on bad input, which the
    /// API's ExceptionHandlingMiddleware normally turns into a 400 — but
    /// that's HTTP middleware and doesn't run for hub invocations, so a bad
    /// call here would otherwise just disconnect the caller with a generic
    /// error. This turns it into the same "RaceError" client event as an
    /// ordinary Result failure instead.
    /// </summary>
    private async Task<TResult?> SendSafely<TResult>(Func<Task<TResult>> send) where TResult : class
    {
        try
        {
            return await send();
        }
        catch (FluentValidation.ValidationException ex)
        {
            var message = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage));
            await Clients.Caller.SendAsync("RaceError", new { error = message, code = "validation_failed" });
            return null;
        }
    }

    private async Task NotifyFriends(Guid userId, string eventName, object payload)
    {
        var friendships = await _friendshipRepository.GetAcceptedForUserAsync(userId);

        var friendIds = friendships
            .Select(f => f.RequesterId == userId ? f.AddresseeId : f.RequesterId)
            .Select(id => id.ToString());

        await Clients.Users(friendIds.ToList()).SendAsync(eventName, payload);
    }

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst("userId")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var id)
            ? id
            : throw new HubException("No valid userId claim on the connection.");
    }
}
