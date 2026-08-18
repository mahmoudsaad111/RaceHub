using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Friends;

namespace RaceHub.Application.Features.Friends.GetPendingRequests;

public record GetPendingRequestsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<PendingFriendRequestDto>>>;
