using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Friends.RespondToFriendRequest;

public record RespondToFriendRequestCommand(
    Guid UserId,
    Guid FriendshipId,
    bool Accept) : IRequest<Result>;
