using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceHub.Application.Features.Friends.GetFriends;
using RaceHub.Application.Features.Friends.GetPendingRequests;
using RaceHub.Application.Features.Friends.RespondToFriendRequest;
using RaceHub.Application.Features.Friends.SendFriendRequest;

namespace RaceHub.API.Controllers;

[Route("api/friends")]
[Authorize]
public class FriendsController : ApiControllerBase
{
    private readonly ISender _sender;

    public FriendsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Accepted friends for the current user.</summary>
    [HttpGet]
    public async Task<IActionResult> GetFriends(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetFriendsQuery(CurrentUserId), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Incoming friend requests awaiting the current user's response.</summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetPendingRequests(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPendingRequestsQuery(CurrentUserId), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>Body: { "addresseeEmail": "..." }</summary>
    [HttpPost("requests")]
    public async Task<IActionResult> SendRequest(
        [FromBody] SendFriendRequestBody body,
        CancellationToken cancellationToken)
    {
        var command = new SendFriendRequestCommand(CurrentUserId, body.AddresseeEmail);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, "Friend request sent.");
    }

    /// <summary>Body: { "accept": true }</summary>
    [HttpPost("requests/{friendshipId:guid}/respond")]
    public async Task<IActionResult> RespondToRequest(
        Guid friendshipId,
        [FromBody] RespondToFriendRequestBody body,
        CancellationToken cancellationToken)
    {
        var command = new RespondToFriendRequestCommand(CurrentUserId, friendshipId, body.Accept);

        var result = await _sender.Send(command, cancellationToken);

        return HandleResult(result, body.Accept ? "Friend request accepted." : "Friend request declined.");
    }

    /// <summary>
    /// Same convention as UsersController/AuthenticationController — reads
    /// the userId claim TokenService bakes into the access token.
    /// </summary>
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

public record SendFriendRequestBody(string AddresseeEmail);

public record RespondToFriendRequestBody(bool Accept);
