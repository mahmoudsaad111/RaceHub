using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Contracts.Events;
using RaceHub.Contracts.Messaging;
using RaceHub.Contracts.Rewards;
using RaceHub.Domain.Entities;
using RaceHub.Domain.Enums;

namespace RaceHub.Application.Features.Races.FinishRace;

public class FinishPlayerCommandHandler : IRequestHandler<FinishPlayerCommand, Result<FinishPlayerResult>>
{
    private readonly IRaceRepository _raceRepository;

    private readonly IOutboxRepository _outboxRepository;
    private readonly IRaceResultRepository _raceResultRepository;
    private readonly IUnitOfWork _unitOfWork;

    public FinishPlayerCommandHandler(
        IRaceRepository raceRepository,
        IOutboxRepository outboxRepository,
        IRaceResultRepository raceResultRepository,
        IUnitOfWork unitOfWork)
    {
        _raceRepository = raceRepository;
        _outboxRepository = outboxRepository;
        _raceResultRepository = raceResultRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FinishPlayerResult>> Handle(FinishPlayerCommand request, CancellationToken cancellationToken)
    {
        var race = await _raceRepository.GetByIdAsync(request.RaceId, cancellationToken);

        if (race is null)
        {
            return Result<FinishPlayerResult>.Failure("Race not found.", "race_not_found");
        }

        var player = race.Players.FirstOrDefault(p => p.UserId == request.UserId);

        if (player is null)
        {
            return Result<FinishPlayerResult>.Failure("You haven't joined this race.", "not_in_race");
        }

        if (player.Status == PlayerRaceStatus.Finished)
        {
            return Result<FinishPlayerResult>.Failure("You have already finished this race.", "already_finished");
        }

        var position = race.Players.Count(p => p.Status == PlayerRaceStatus.Finished) + 1;
        var totalTime = TimeSpan.FromMilliseconds(request.TotalTimeMs);

        player.Finish(position, totalTime);

        // Same single source of truth RewardWorker uses when it credits the
        // actual balance — keeping the Results-screen snapshot and the async
        // credit from ever drifting apart (see RewardCurve).
        var (coins, experience) = RewardCurve.ForPosition(position);

        var raceResult = new RaceResult(
            race.Id,
            player.UserId,
            position,
            totalTime,
            player.BestLapTime,
            experience,
            coins);

        await _raceResultRepository.AddAsync(raceResult, cancellationToken);

        var raceFinished = race.Players.All(p =>
            p.Status == PlayerRaceStatus.Finished || p.Status == PlayerRaceStatus.Disconnected);
        RaceFinishedDto? finalResults = null;

        if (raceFinished)
        {
            race.Finish();

            var standings = race.Players
                // A Disconnected player abandoned the race without
                // finishing, so they never got a FinishingPosition —
                // include them here and p.FinishingPosition!.Value below
                // throws. They're left out of the results table entirely
                // rather than shown with a fabricated position/time.
                .Where(p => p.Status == PlayerRaceStatus.Finished)
                .OrderBy(p => p.FinishingPosition)
                .Select(p =>
                {
                    var (rowCoins, rowExperience) = RewardCurve.ForPosition(p.FinishingPosition!.Value);

                    return new RaceResultRowDto(
                        p.FinishingPosition!.Value,
                        p.UserId,
                        p.User.DisplayName,
                        (int)p.TotalRaceTime!.Value.TotalMilliseconds,
                        p.BestLapTime is null ? null : (int)p.BestLapTime.Value.TotalMilliseconds,
                        rowExperience,
                        rowCoins);
                })
                .ToList();
            var integrationEvent = new RaceFinishedIntegrationEvent
            {
                RaceId = race.Id,
                TrackId = race.TrackId,
                Results = standings
                .Select(s => new RaceFinishedIntegrationEvent.PlayerResult(s.UserId, s.Position, s.TotalTimeMs))
                .ToList(),
            };

            finalResults = new RaceFinishedDto(race.Id, standings);
            await _outboxRepository.AddAsync(
          OutboxMessage.Create(OutboxMessageTypes.RaceFinished, integrationEvent),
           cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var playerFinishedDto = new PlayerFinishedDto(
            request.RaceId,
            player.UserId,
            position,
            request.TotalTimeMs,
            player.BestLapTime is null ? null : (int)player.BestLapTime.Value.TotalMilliseconds);

        return Result<FinishPlayerResult>.Success(
            new FinishPlayerResult(playerFinishedDto, raceFinished, finalResults));
    }
}
