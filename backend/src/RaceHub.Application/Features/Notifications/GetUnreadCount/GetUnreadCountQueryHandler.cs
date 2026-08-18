using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Notifications.GetUnreadCount;

public class GetUnreadCountQueryHandler
    : IRequestHandler<GetUnreadCountQuery, Result<int>>
{
    private readonly INotificationRepository _notificationRepository;

    public GetUnreadCountQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<Result<int>> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var count = await _notificationRepository.GetUnreadCountAsync(request.UserId, cancellationToken);
        return Result<int>.Success(count);
    }
}
