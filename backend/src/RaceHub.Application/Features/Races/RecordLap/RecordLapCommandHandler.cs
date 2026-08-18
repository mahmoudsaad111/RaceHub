using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Enums;

namespace RaceHub.Application.Features.Races.RecordLap;

public class RecordLapCommandHandler : IRequestHandler<RecordLapCommand, Result<PlayerLapDto>>
{
    private readonly IRaceRepository _raceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordLapCommandHandler(IRaceRepository raceRepository, IUnitOfWork unitOfWork)
    {
        _raceRepository = raceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PlayerLapDto>> Handle(RecordLapCommand request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result<PlayerLapDto>.Failure("Race not found.", "race_not_found");
        }

        if (race.Status != RaceStatus.Running)
        {
            return Result<PlayerLapDto>.Failure("Race is not currently running.", "race_not_running");
        }

        var player = race.Players.FirstOrDefault(p => p.UserId == request.UserId);

        if (player is null)
        {
            return Result<PlayerLapDto>.Failure("You haven't joined this race.", "not_in_race");
        }

        player.CompleteLap(request.LapNumber, TimeSpan.FromMilliseconds(request.LapTimeMs));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new PlayerLapDto(
            request.RaceId,
            player.UserId,
            request.LapNumber,
            request.LapTimeMs,
            (int)player.BestLapTime!.Value.TotalMilliseconds);

        return Result<PlayerLapDto>.Success(dto);
    }
}
