using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Races.DeleteRace;

public class DeleteRaceCommandHandler : IRequestHandler<DeleteRaceCommand, Result>
{
    private readonly IRaceRepository _raceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRaceCommandHandler(IRaceRepository raceRepository, IUnitOfWork unitOfWork)
    {
        _raceRepository = raceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteRaceCommand request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result.Failure("Race not found.", "race_not_found");
        }

        if (race.HostUserId != request.RequestingUserId)
        {
            return Result.Failure("Only the host can delete this room.", "forbidden");
        }

        _raceRepository.Remove(race);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
