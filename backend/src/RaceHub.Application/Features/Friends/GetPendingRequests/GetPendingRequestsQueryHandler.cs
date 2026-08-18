using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Friends;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Friends.GetPendingRequests;

public class GetPendingRequestsQueryHandler
    : IRequestHandler<GetPendingRequestsQuery, Result<IReadOnlyList<PendingFriendRequestDto>>>
{
    private readonly IFriendshipRepository _friendshipRepository;

    public GetPendingRequestsQueryHandler(IFriendshipRepository friendshipRepository)
    {
        _friendshipRepository = friendshipRepository;
    }

    public async Task<Result<IReadOnlyList<PendingFriendRequestDto>>> Handle(
        GetPendingRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var pending = await _friendshipRepository.GetPendingIncomingAsync(request.UserId, cancellationToken);

        var dtos = pending
            .Select(f => new PendingFriendRequestDto(
                f.Id,
                f.RequesterId,
                f.Requester.DisplayName,
                f.CreatedAtUtc))
            .ToList();

        return Result<IReadOnlyList<PendingFriendRequestDto>>.Success(dtos);
    }
}
