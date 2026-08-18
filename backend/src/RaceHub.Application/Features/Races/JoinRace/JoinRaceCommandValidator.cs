using FluentValidation;

namespace RaceHub.Application.Features.Races.JoinRace;

public class JoinRaceCommandValidator : AbstractValidator<JoinRaceCommand>
{
    public JoinRaceCommandValidator()
    {
        RuleFor(x => x.RaceId).NotEmpty();
        RuleFor(x => x.CarId).NotEmpty();
    }
}
