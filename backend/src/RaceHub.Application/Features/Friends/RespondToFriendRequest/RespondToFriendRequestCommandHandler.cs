using MediatR;
using Microsoft.AspNetCore.Identity;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.Domain.Enums;

namespace RaceHub.Application.Features.Friends.RespondToFriendRequest;

public class RespondToFriendRequestCommandHandler : IRequestHandler<RespondToFriendRequestCommand, Result>
{
    private readonly IFriendshipRepository _friendshipRepository;
    private readonly UserManager<User> _userManager;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RespondToFriendRequestCommandHandler(
        IFriendshipRepository friendshipRepository,
        UserManager<User> userManager,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork)
    {
        _friendshipRepository = friendshipRepository;
        _userManager = userManager;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RespondToFriendRequestCommand request, CancellationToken cancellationToken)
    {
        var friendship = await _friendshipRepository.GetByIdAsync(request.FriendshipId, cancellationToken);

        if (friendship is null)
        {
            return Result.Failure("Friend request not found.", "friendship_not_found");
        }

        if (friendship.AddresseeId != request.UserId)
        {
            return Result.Failure("You can't respond to this request.", "forbidden");
        }

        if (friendship.Status != FriendshipStatus.Pending)
        {
            return Result.Failure("This request has already been responded to.", "conflict");
        }

        if (request.Accept)
        {
            friendship.Accept();

            var addressee = await _userManager.FindByIdAsync(request.UserId.ToString());
            var notification = new Notification(
                friendship.RequesterId,
                "friend_accepted",
                "Friend Request Accepted",
                $"{addressee?.DisplayName ?? "Someone"} accepted your friend request.",
                friendship.Id.ToString());

            await _notificationRepository.AddAsync(notification, cancellationToken);
        }
        else
        {
            friendship.Decline();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
