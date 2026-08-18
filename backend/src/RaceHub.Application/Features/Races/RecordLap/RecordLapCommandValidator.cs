using FluentValidation;

namespace RaceHub.Application.Features.Races.RecordLap;

public class RecordLapCommandValidator : AbstractValidator<RecordLapCommand>
{
    public RecordLapCommandValidator()
    {
        RuleFor(x => x.LapNumber).GreaterThan(0);
        RuleFor(x => x.LapTimeMs).GreaterThan(0);
    }
}
