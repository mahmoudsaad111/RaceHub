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
using RaceHub.Contracts.Events;
using RaceHub.Contracts.Messaging;

namespace RaceHub.API.Messaging;

/// <summary>
/// Consumes <c>achievement.unlocked</c> events from
/// <c>achievement-notify-queue</c> and relays them to the affected user's
/// connected client(s) via SignalR — the same best-effort / at-most-once
/// pattern as RewardNotificationRelayService. The UserAchievement row and
/// its Notification were already committed by AchievementsWorker before
/// this event was published, so dropping this relay only means the client
/// misses the live toast, never the unlock itself.
/// </summary>
public class AchievementNotificationRelayService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly ILogger<AchievementNotificationRelayService> _logger;

    public AchievementNotificationRelayService(
        IServiceScopeFactory scopeFactory,
        IConnection connection,
        ILogger<AchievementNotificationRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _connection = connection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            RaceEventsTopology.Queues.AchievementNotify,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) => await OnMessageAsync(channel, ea, stoppingToken);

        await channel.BasicConsumeAsync(
            RaceEventsTopology.Queues.AchievementNotify,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        try
        {
            if (ea.RoutingKey != RaceEventsTopology.AchievementUnlockedRoutingKey)
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                return;
            }

            var payload = Encoding.UTF8.GetString(ea.Body.Span);
            var evt = JsonSerializer.Deserialize<AchievementUnlockedIntegrationEvent>(payload);

            if (evt is null)
            {
                _logger.LogWarning("Malformed AchievementUnlocked payload received.");
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<RaceHubHub>>();

            await hubContext.Clients.User(evt.UserId.ToString()).SendAsync(
                "AchievementUnlocked",
                new
                {
                    userId = evt.UserId,
                    achievementKey = evt.AchievementKey,
                    title = evt.Title,
                    description = evt.Description,
                },
                stoppingToken);

            await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error relaying AchievementUnlocked event.");
            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
        }
    }
}
