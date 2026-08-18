using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Features.Races.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Races.JoinRace;

public class JoinRaceCommandHandler : IRequestHandler<JoinRaceCommand, Result<RaceDetailDto>>
{
    private readonly IRaceRepository _raceRepository;
    private readonly ICarRepository _carRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JoinRaceCommandHandler> _logger;

    public JoinRaceCommandHandler(
        IRaceRepository raceRepository,
        ICarRepository carRepository,
        IUnitOfWork unitOfWork,
        ILogger<JoinRaceCommandHandler> logger)
    {
        _raceRepository = raceRepository;
        _carRepository = carRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<RaceDetailDto>> Handle(JoinRaceCommand request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result<RaceDetailDto>.Failure("Race not found.", "race_not_found");
        }

        var car = await _carRepository.GetByIdAsync(request.CarId, cancellationToken);

        if (car is null)
        {
            return Result<RaceDetailDto>.Failure("Car not found.", "car_not_found");
        }

        if (race.Players.Any(p => p.UserId == request.UserId))
        {
            return Result<RaceDetailDto>.Failure(
                "You have already joined this race.",
                "already_in_race");
        }

        try
        {
            var player = race.AddPlayer(request.UserId, request.CarId);
            _raceRepository.AddPlayer(player);
        }
        catch (InvalidOperationException ex)
        {
            return Result<RaceDetailDto>.Failure(ex.Message, "race_join_failed");
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<RaceDetailDto>.Failure(
                "You have already joined this race.",
                "already_in_race");
        }

        var hydrated = await _raceRepository.GetSnapshotByIdAsync(race.Id, cancellationToken);

        _logger.LogInformation(
            "JoinRace succeeded: RaceId={RaceId}, UserId={UserId}, PlayersCount={PlayersCount}, PlayerUserIds={PlayerUserIds}",
            race.Id,
            request.UserId,
            hydrated!.Players.Count,
            string.Join(", ", hydrated.Players.Select(p => p.UserId)));

        return Result<RaceDetailDto>.Success(RaceMapper.ToDetailDto(hydrated));
    }
}
