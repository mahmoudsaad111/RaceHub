using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RaceHub.API.Hubs;
using RaceHubHub = RaceHub.API.Hubs.RaceHub;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Contracts.Events;
using RaceHub.Contracts.Messaging;
using RaceHub.Domain.Entities;

namespace RaceHub.API.Messaging;

/// <summary>
/// Consumes <c>reward.credited</c> events from <c>reward-notify-queue</c>
/// and relays them to the affected user's connected client(s) via SignalR.
/// Best-effort / at-most-once: if the API is down when the event arrives,
/// the queue holds it until the API comes back. The actual Coins/Experience
/// update was already committed by RewardWorker before this event was
/// published, so dropping this relay only means the client misses the toast.
/// </summary>
public class RewardNotificationRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly ILogger<RewardNotificationRelayService> _logger;

    public RewardNotificationRelayService(
        IServiceScopeFactory scopeFactory,
        IConnection connection,
        ILogger<RewardNotificationRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _connection = connection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            RaceEventsTopology.Queues.RewardNotify,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) => await OnMessageAsync(channel, ea, stoppingToken);

        await channel.BasicConsumeAsync(
            RaceEventsTopology.Queues.RewardNotify,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        try
        {
            if (ea.RoutingKey != RaceEventsTopology.RewardCreditedRoutingKey)
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                return;
            }

            var payload = Encoding.UTF8.GetString(ea.Body.Span);
            var evt = JsonSerializer.Deserialize<RewardCreditedIntegrationEvent>(payload);

            if (evt is null)
            {
                _logger.LogWarning("Malformed RewardCredited payload received.");
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<RaceHubHub>>();

            await hubContext.Clients.User(evt.UserId.ToString()).SendAsync(
                "RewardCredited",
                new
                {
                    userId = evt.UserId,
                    coinsAwarded = evt.CoinsAwarded,
                    experienceAwarded = evt.ExperienceAwarded,
                    totalCoins = evt.TotalCoins,
                    totalExperience = evt.TotalExperience,
                    leveledUp = evt.LeveledUp,
                    newLevel = evt.NewLevel,
                },
                stoppingToken);

            await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error relaying RewardCredited event.");
            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
        }
    }
}
