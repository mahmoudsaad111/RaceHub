using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Features.Races.Common;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Enums;

namespace RaceHub.Application.Features.Races.ChangePlayerCar;

public class ChangePlayerCarCommandHandler : IRequestHandler<ChangePlayerCarCommand, Result<RaceDetailDto>>
{
    private readonly IRaceRepository _raceRepository;
    private readonly ICarRepository _carRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePlayerCarCommandHandler(
        IRaceRepository raceRepository,
        ICarRepository carRepository,
        IUnitOfWork unitOfWork)
    {
        _raceRepository = raceRepository;
        _carRepository = carRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RaceDetailDto>> Handle(ChangePlayerCarCommand request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);
        if (race is null)
            return Result<RaceDetailDto>.Failure("Race not found.", "race_not_found");

        if (race.Status != RaceStatus.Waiting)
            return Result<RaceDetailDto>.Failure("Cars can only be changed before the race starts.", "race_not_waiting");

        var player = race.Players.FirstOrDefault(p => p.UserId == request.UserId);
        if (player is null)
            return Result<RaceDetailDto>.Failure("You haven't joined this race.", "not_in_race");

        var car = await _carRepository.GetByIdAsync(request.CarId, cancellationToken);
        if (car is null || !car.IsActive)
            return Result<RaceDetailDto>.Failure("Car not found.", "car_not_found");

        try
        {
            player.ChangeCar(car.Id);
        }
        catch (InvalidOperationException ex)
        {
            return Result<RaceDetailDto>.Failure(ex.Message, "player_is_ready");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var hydrated = await _raceRepository.GetSnapshotByIdAsync(race.Id, cancellationToken);

        return Result<RaceDetailDto>.Success(RaceMapper.ToDetailDto(hydrated!));
    }
}
