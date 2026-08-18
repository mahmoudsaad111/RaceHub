using System.Text.Json;
using RabbitMQ.Client;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Contracts.Events;
using RaceHub.Contracts.Messaging;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Messaging;

namespace RaceHub.StatisticsWorker;

public class StatisticsConsumer : IdempotentConsumer
{
    public StatisticsConsumer(IConnection connection, IServiceScopeFactory scopeFactory, ILogger<StatisticsConsumer> logger)
        : base(connection, scopeFactory, logger, RaceEventsTopology.Queues.Statistics, nameof(StatisticsConsumer)) { }

    protected override async Task HandleAsync(string routingKey, string payload, IServiceProvider scopeProvider, CancellationToken ct)
    {
        if (routingKey != RaceEventsTopology.RaceFinishedRoutingKey) return;

        var evt = JsonSerializer.Deserialize<RaceFinishedIntegrationEvent>(payload)
            ?? throw new InvalidOperationException("Malformed RaceFinished payload");

        var history = scopeProvider.GetRequiredService<IRaceHistoryRepository>();

        foreach (var result in evt.Results)
        {
            await history.AddAsync(new RaceHistoryEntry(result.UserId, evt.RaceId, evt.TrackId, result.Position, result.FinishTimeMs), ct);
        }
    }
}