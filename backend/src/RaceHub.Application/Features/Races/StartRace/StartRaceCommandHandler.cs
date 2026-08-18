using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Features.Races.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Races.StartRace;

public class StartRaceCommandHandler : IRequestHandler<StartRaceCommand, Result<RaceDetailDto>>
{
    private readonly IRaceRepository _raceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StartRaceCommandHandler(IRaceRepository raceRepository, IUnitOfWork unitOfWork)
    {
        _raceRepository = raceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RaceDetailDto>> Handle(StartRaceCommand request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result<RaceDetailDto>.Failure("Race not found.", "race_not_found");
        }

        if (race.HostUserId != request.RequestingUserId)
        {
            return Result<RaceDetailDto>.Failure("Only the host can start the race.", "forbidden");
        }

        if (!race.AllPlayersReady())
        {
            return Result<RaceDetailDto>.Failure("All players must be ready before starting.", "not_all_ready");
        }

        try
        {
            race.Start();
        }
        catch (InvalidOperationException ex)
        {
            return Result<RaceDetailDto>.Failure(ex.Message, "invalid_race_state");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var hydrated = await _raceRepository.GetSnapshotByIdAsync(race.Id, cancellationToken);

        return Result<RaceDetailDto>.Success(RaceMapper.ToDetailDto(hydrated!));
    }
}
