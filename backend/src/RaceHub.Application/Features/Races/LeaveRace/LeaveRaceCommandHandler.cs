using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.Features.Races.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Races.LeaveRace;

public class LeaveRaceCommandHandler : IRequestHandler<LeaveRaceCommand, Result<LeaveRaceResult>>
{
    private readonly IRaceRepository _raceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public LeaveRaceCommandHandler(IRaceRepository raceRepository, IUnitOfWork unitOfWork)
    {
        _raceRepository = raceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LeaveRaceResult>> Handle(LeaveRaceCommand request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result<LeaveRaceResult>.Failure("Race not found.", "race_not_found");
        }

        var wasHost = race.HostUserId == request.UserId;

        race.RemovePlayer(request.UserId);

        if (race.Players.Count == 0)
        {
            // Last player out — no point keeping an empty room around for
            // the lobby list to keep showing.
            _raceRepository.Remove(race);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<LeaveRaceResult>.Success(new LeaveRaceResult(RoomClosed: true, RaceDetail: null));
        }

        if (wasHost)
        {
            race.TransferHost(race.Players.First().UserId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var hydrated = await _raceRepository.GetSnapshotByIdAsync(race.Id, cancellationToken);

        return Result<LeaveRaceResult>.Success(
            new LeaveRaceResult(RoomClosed: false, RaceDetail: RaceMapper.ToDetailDto(hydrated!)));
    }
}
