using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Notifications;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Notifications.GetNotifications;

public record GetNotificationsQuery(Guid UserId, bool UnreadOnly = false)
    : IRequest<Result<IReadOnlyList<NotificationDto>>>;
