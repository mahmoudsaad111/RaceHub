using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Friends.SendFriendRequest;

public record SendFriendRequestCommand(
    Guid RequesterId,
    string AddresseeEmail) : IRequest<Result>;
