using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Notifications.GetUnreadCount;

public record GetUnreadCountQuery(Guid UserId)
    : IRequest<Result<int>>;
