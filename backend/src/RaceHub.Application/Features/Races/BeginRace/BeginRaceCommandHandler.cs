using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Races.BeginRace;

public class BeginRaceCommandHandler : IRequestHandler<BeginRaceCommand, Result>
{
    private readonly IRaceRepository _raceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BeginRaceCommandHandler(IRaceRepository raceRepository, IUnitOfWork unitOfWork)
    {
        _raceRepository = raceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(BeginRaceCommand request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result.Failure("Race not found.", "race_not_found");
        }

        try
        {
            race.Begin();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message, "invalid_race_state");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
