using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Features.Races.Common;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Races.CreateRace;

public class CreateRaceCommandHandler : IRequestHandler<CreateRaceCommand, Result<RaceDetailDto>>
{
    private readonly IRaceRepository _raceRepository;
    private readonly ITrackRepository _trackRepository;
    private readonly ICarRepository _carRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRaceCommandHandler(
        IRaceRepository raceRepository,
        ITrackRepository trackRepository,
        ICarRepository carRepository,
        IUnitOfWork unitOfWork)
    {
        _raceRepository = raceRepository;
        _trackRepository = trackRepository;
        _carRepository = carRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RaceDetailDto>> Handle(CreateRaceCommand request, CancellationToken cancellationToken)
    {
        var track = await _trackRepository.GetByIdAsync(request.TrackId, cancellationToken);

        if (track is null)
        {
            return Result<RaceDetailDto>.Failure("Track not found.", "track_not_found");
        }

        var car = await _carRepository.GetByIdAsync(request.CarId, cancellationToken);

        if (car is null)
        {
            return Result<RaceDetailDto>.Failure("Car not found.", "car_not_found");
        }

        var race = new Race(track.Id, request.HostUserId, request.MaxPlayers, track.TotalLaps);
        race.AddPlayer(request.HostUserId, request.CarId);

        await _raceRepository.AddAsync(race, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload through the repository's tracked query so the Track/User/Car
        // navigations needed by RaceMapper are populated — the in-memory
        // `race` above only has the ids we set on it manually.
        var hydrated = await _raceRepository.GetSnapshotByIdAsync(race.Id, cancellationToken);

        return Result<RaceDetailDto>.Success(RaceMapper.ToDetailDto(hydrated!));
    }
}
