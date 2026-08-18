using System.Text.Json;
using RabbitMQ.Client;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Contracts.Events;
using RaceHub.Contracts.Messaging;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Messaging;

namespace RaceHub.RankingWorker;

public class RankingConsumer : IdempotentConsumer
{
    public RankingConsumer(IConnection connection, IServiceScopeFactory scopeFactory, ILogger<RankingConsumer> logger)
        : base(connection, scopeFactory, logger, RaceEventsTopology.Queues.Ranking, nameof(RankingConsumer)) { }

    protected override async Task HandleAsync(string routingKey, string payload, IServiceProvider scopeProvider, CancellationToken ct)
    {
        if (routingKey != RaceEventsTopology.RaceFinishedRoutingKey) return;

        var evt = JsonSerializer.Deserialize<RaceFinishedIntegrationEvent>(payload)
            ?? throw new InvalidOperationException("Malformed RaceFinished payload");

        var statsRepository = scopeProvider.GetRequiredService<IPlayerStatisticsRepository>();

        // Load (or create) every participant's stats first, so the field's
        // average rating used below is a snapshot from *before* this
        // race's changes are applied to any of them — the order this
        // foreach runs in must not affect the outcome.
        var statsByUser = new Dictionary<Guid, PlayerStatistics>();

        foreach (var result in evt.Results)
        {
            var stats = await statsRepository.GetByUserIdAsync(result.UserId, ct);

            if (stats is null)
            {
                stats = new PlayerStatistics(result.UserId);
                await statsRepository.AddAsync(stats, ct);
            }

            statsByUser[result.UserId] = stats;
        }

        var fieldAverageRating = statsByUser.Values.Average(s => s.RatingPoints);
        var fieldSize = evt.Results.Count;

        foreach (var result in evt.Results)
        {
            statsByUser[result.UserId].RecordRaceResult(result.Position, result.FinishTimeMs, fieldAverageRating, fieldSize);
        }
    }
}
