using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Notifications;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Notifications.GetNotifications;

public class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, Result<IReadOnlyList<NotificationDto>>>
{
    private readonly INotificationRepository _notificationRepository;

    public GetNotificationsQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<Result<IReadOnlyList<NotificationDto>>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var notifications = await _notificationRepository.GetForUserAsync(request.UserId, request.UnreadOnly, cancellationToken);

        var dtos = notifications.Select(n => new NotificationDto(
            n.Id,
            n.Type,
            n.Title,
            n.Message,
            n.Data,
            n.IsRead,
            n.CreatedAtUtc)).ToList();

        return Result<IReadOnlyList<NotificationDto>>.Success(dtos);
    }
}
