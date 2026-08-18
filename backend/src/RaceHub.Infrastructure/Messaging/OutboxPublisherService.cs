// RaceHub.Infrastructure/Messaging/OutboxPublisherService.cs
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RaceHub.Contracts.Messaging;
using RaceHub.Infrastructure.Persistence;
namespace RaceHub.Infrastructure.Messaging;
public class OutboxPublisherService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int MaxAttempts = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(IServiceScopeFactory scopeFactory, IConnection connection, ILogger<OutboxPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _connection = connection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PublishPendingAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Outbox publish loop failed"); }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var pending = await context.OutboxMessages
        .Where(m => m.ProcessedOnUtc == null && m.Attempts < MaxAttempts)
        .OrderBy(m => m.OccurredOnUtc)
        .Take(50)
        .ToListAsync(ct);

    if (pending.Count == 0) return;

    var channelOptions = new CreateChannelOptions(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    await using var channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken: ct);

    foreach (var message in pending)
    {
        try
        {
            var props = new BasicProperties { Persistent = true, MessageId = message.Id.ToString() };
            var body = Encoding.UTF8.GetBytes(message.Payload);

            // With confirms enabled, this await doesn't complete until the
            // broker has durably confirmed the message — reaching the next
            // line means it's actually there, not just that we called a
            // method and hoped. A nack/return throws PublishException
            // instead, caught below like any other publish failure.
            await channel.BasicPublishAsync(
                exchange: RaceEventsTopology.ExchangeName,
                routingKey: OutboxMessageTypes.RoutingKeyFor(message.Type),
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);

            message.MarkProcessed();
        }
        catch (Exception ex)
        {
            message.MarkFailed(ex.Message);
            _logger.LogWarning(ex, "Outbox publish failed for {MessageId}, attempt {Attempts}", message.Id, message.Attempts);
        }
    }

    await context.SaveChangesAsync(ct);
}
}