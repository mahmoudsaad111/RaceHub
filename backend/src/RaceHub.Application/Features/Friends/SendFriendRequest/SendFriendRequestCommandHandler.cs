using MediatR;
using Microsoft.AspNetCore.Identity;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.Domain.Enums;

namespace RaceHub.Application.Features.Friends.SendFriendRequest;

public class SendFriendRequestCommandHandler : IRequestHandler<SendFriendRequestCommand, Result>
{
    private readonly UserManager<User> _userManager;
    private readonly IFriendshipRepository _friendshipRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SendFriendRequestCommandHandler(
        UserManager<User> userManager,
        IFriendshipRepository friendshipRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _friendshipRepository = friendshipRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SendFriendRequestCommand request, CancellationToken cancellationToken)
    {
        var addressee = await _userManager.FindByEmailAsync(request.AddresseeEmail);

        if (addressee is null)
        {
            return Result.Failure("No user found with that email.", "user_not_found");
        }

        if (addressee.Id == request.RequesterId)
        {
            return Result.Failure("You can't send a friend request to yourself.", "invalid_request");
        }

        var existing = await _friendshipRepository.GetBetweenAsync(
            request.RequesterId, addressee.Id, cancellationToken);

        if (existing is not null)
        {
            return existing.Status switch
            {
                FriendshipStatus.Accepted => Result.Failure("You're already friends.", "already_friends"),
                FriendshipStatus.Pending => Result.Failure("A friend request is already pending.", "request_pending"),
                _ => Result.Failure("A relationship with this user already exists.", "conflict"),
            };
        }

        var friendship = new Friendship(request.RequesterId, addressee.Id);

        await _friendshipRepository.AddAsync(friendship, cancellationToken);

        var requester = await _userManager.FindByIdAsync(request.RequesterId.ToString());
        var notification = new Notification(
            addressee.Id,
            "friend_request",
            "New Friend Request",
            $"{requester?.DisplayName ?? "Someone"} sent you a friend request.",
            friendship.Id.ToString());

        await _notificationRepository.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
