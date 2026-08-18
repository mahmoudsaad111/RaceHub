using MediatR;
using Microsoft.Extensions.Logging;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Features.Races.Common;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Enums;

namespace RaceHub.Application.Features.Races.SetPlayerReady;

public class SetPlayerReadyCommandHandler : IRequestHandler<SetPlayerReadyCommand, Result<RaceDetailDto>>
{
    private readonly IRaceRepository _raceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetPlayerReadyCommandHandler> _logger;

    public SetPlayerReadyCommandHandler(IRaceRepository raceRepository, IUnitOfWork unitOfWork, ILogger<SetPlayerReadyCommandHandler> logger)
    {
        _raceRepository = raceRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<RaceDetailDto>> Handle(SetPlayerReadyCommand request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result<RaceDetailDto>.Failure("Race not found.", "race_not_found");
        }

        var player = race.Players.FirstOrDefault(p => p.UserId == request.UserId);

        if (player is null)
        {
            _logger.LogWarning(
                "SetPlayerReady failed: player not found. RaceId={RaceId}, UserId={UserId}, PlayersCount={PlayersCount}, PlayerUserIds={PlayerUserIds}",
                request.RaceId,
                request.UserId,
                race.Players.Count,
                string.Join(", ", race.Players.Select(p => p.UserId)));

            return Result<RaceDetailDto>.Failure("You haven't joined this race.", "not_in_race");
        }

        if (race.Status != RaceStatus.Waiting)
        {
            return Result<RaceDetailDto>.Failure(
                "Readiness can only be changed before the race starts.",
                "race_not_waiting");
        }

        try
        {
            player.ToggleReady();
        }
        catch (InvalidOperationException ex)
        {
            return Result<RaceDetailDto>.Failure(ex.Message, "invalid_player_state");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var hydrated = await _raceRepository.GetSnapshotByIdAsync(race.Id, cancellationToken);

        return Result<RaceDetailDto>.Success(RaceMapper.ToDetailDto(hydrated!));
    }
}
