using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceHub.Application.DTOs.Notifications;
using RaceHub.Application.Features.Notifications.GetNotifications;
using RaceHub.Application.Features.Notifications.GetUnreadCount;
using RaceHub.Application.Features.Notifications.MarkAllAsRead;
using RaceHub.Application.Features.Notifications.MarkAsRead;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.API.Controllers;

[Route("api/notifications")]
[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetNotificationsQuery(CurrentUserId, unreadOnly), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetUnreadCountQuery(CurrentUserId), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkNotificationAsReadCommand(notificationId, CurrentUserId), cancellationToken);
        return HandleResult(result, "Notification marked as read.");
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new MarkAllNotificationsAsReadCommand(CurrentUserId), cancellationToken);
        return HandleResult(result, "All notifications marked as read.");
    }

    private Guid CurrentUserId
    {
        get
        {
            var claim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(claim, out var id)
                ? id
                : throw new UnauthorizedAccessException("No valid userId claim on the access token.");
        }
    }
}
