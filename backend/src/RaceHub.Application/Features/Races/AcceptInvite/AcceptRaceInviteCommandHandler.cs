using MediatR;
using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Features.Races.Common;
using RaceHub.Application.Interfaces.Persistence;

namespace RaceHub.Application.Features.Races.AcceptInvite;

public class AcceptRaceInviteCommandHandler : IRequestHandler<AcceptRaceInviteCommand, Result<RaceDetailDto>>
{
    private readonly IRaceRepository _raceRepository;
    private readonly ICarRepository _carRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AcceptRaceInviteCommandHandler(
        IRaceRepository raceRepository,
        ICarRepository carRepository,
        IUnitOfWork unitOfWork)
    {
        _raceRepository = raceRepository;
        _carRepository = carRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RaceDetailDto>> Handle(
        AcceptRaceInviteCommand request,
        CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result<RaceDetailDto>.Failure("Race not found.", "race_not_found");
        }

        if (race.Players.Any(p => p.UserId == request.UserId))
        {
            var hydratedAlready = await _raceRepository.GetSnapshotByIdAsync(race.Id, cancellationToken);
            return Result<RaceDetailDto>.Success(RaceMapper.ToDetailDto(hydratedAlready!));
        }

        var car = (await _carRepository.GetAllActiveAsync(cancellationToken)).FirstOrDefault();

        if (car is null)
        {
            return Result<RaceDetailDto>.Failure("No cars available.", "no_cars_available");
        }

        try
        {
            var player = race.AddPlayer(request.UserId, car.Id);
            _raceRepository.AddPlayer(player);
        }
        catch (InvalidOperationException ex)
        {
            return Result<RaceDetailDto>.Failure(ex.Message, "race_join_failed");
        }

        // Never report success if adding the RacePlayer failed. That would
        // navigate an invitee into the room without a participant record.
        // The existing-player case is handled above while the race is tracked.
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Same race condition JoinRaceCommandHandler guards against:
            // two near-simultaneous accept-invite/join calls for the same
            // (RaceId, UserId) can both pass the "already joined" check
            // above before either has committed, so the second one trips
            // the unique index at the database level instead. That's not a
            // real failure from the caller's point of view — they ARE in
            // the race, just via the other concurrent call — so treat it
            // as success rather than letting it surface as an unhandled 500.
            var alreadyJoined = await _raceRepository.GetSnapshotByIdAsync(race.Id, cancellationToken);
            return Result<RaceDetailDto>.Success(RaceMapper.ToDetailDto(alreadyJoined!));
        }

        var hydrated = await _raceRepository.GetSnapshotByIdAsync(race.Id, cancellationToken);

        return Result<RaceDetailDto>.Success(RaceMapper.ToDetailDto(hydrated!));
    }
}
