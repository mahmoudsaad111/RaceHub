using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Friends;

namespace RaceHub.Application.Features.Friends.GetFriends;

public record GetFriendsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<FriendDto>>>;
