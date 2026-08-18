using FluentValidation;

namespace RaceHub.Application.Features.Races.FinishRace;

public class FinishPlayerCommandValidator : AbstractValidator<FinishPlayerCommand>
{
    public FinishPlayerCommandValidator()
    {
        RuleFor(x => x.TotalTimeMs).GreaterThan(0);
    }
}
