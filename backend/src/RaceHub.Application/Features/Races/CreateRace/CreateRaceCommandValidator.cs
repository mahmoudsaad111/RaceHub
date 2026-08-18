using FluentValidation;

namespace RaceHub.Application.Features.Races.CreateRace;

public class CreateRaceCommandValidator : AbstractValidator<CreateRaceCommand>
{
    public CreateRaceCommandValidator()
    {
        RuleFor(x => x.TrackId).NotEmpty();
        RuleFor(x => x.CarId).NotEmpty();
        RuleFor(x => x.MaxPlayers).InclusiveBetween(2, 8);
    }
}
