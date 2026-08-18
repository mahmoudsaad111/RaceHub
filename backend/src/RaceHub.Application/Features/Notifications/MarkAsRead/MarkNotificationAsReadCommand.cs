using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Notifications.MarkAsRead;

public record MarkNotificationAsReadCommand(Guid NotificationId, Guid UserId)
    : IRequest<Result>;
