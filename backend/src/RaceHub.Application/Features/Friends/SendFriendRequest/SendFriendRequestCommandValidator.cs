using FluentValidation;

namespace RaceHub.Application.Features.Friends.SendFriendRequest;

public class SendFriendRequestCommandValidator : AbstractValidator<SendFriendRequestCommand>
{
    public SendFriendRequestCommandValidator()
    {
        RuleFor(x => x.AddresseeEmail)
            .NotEmpty()
            .EmailAddress();
    }
}
