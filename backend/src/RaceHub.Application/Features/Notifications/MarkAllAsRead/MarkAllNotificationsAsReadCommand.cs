using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Notifications.MarkAllAsRead;

public record MarkAllNotificationsAsReadCommand(Guid UserId)
    : IRequest<Result>;
