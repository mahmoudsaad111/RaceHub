using System.Text.Json;
using RabbitMQ.Client;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Contracts.Events;
using RaceHub.Contracts.Messaging;
using RaceHub.Contracts.Rewards;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Messaging;

namespace RaceHub.RewardWorker;

public class RewardConsumer : IdempotentConsumer
{
    public RewardConsumer(IConnection connection, IServiceScopeFactory scopeFactory, ILogger<RewardConsumer> logger)
        : base(connection, scopeFactory, logger, RaceEventsTopology.Queues.Reward, nameof(RewardConsumer)) { }

    protected override async Task HandleAsync(string routingKey, string payload, IServiceProvider scopeProvider, CancellationToken ct)
    {
        if (routingKey != RaceEventsTopology.RaceFinishedRoutingKey) return;

        var evt = JsonSerializer.Deserialize<RaceFinishedIntegrationEvent>(payload)
            ?? throw new InvalidOperationException("Malformed RaceFinished payload");

        var users = scopeProvider.GetRequiredService<IUserRewardRepository>();
        var notifications = scopeProvider.GetRequiredService<INotificationRepository>();
        var channel = scopeProvider.GetRequiredService<IConnection>();
        var previousLevels = new Dictionary<Guid, int>();

        foreach (var result in evt.Results)
        {
            var (coins, xp) = RewardCurve.ForPosition(result.Position);
            var user = await users.GetByIdAsync(result.UserId, ct);

            if (user is null) continue;

            var oldLevel = LevelCalculator.GetLevel(user.Experience);
            previousLevels[user.Id] = oldLevel;

            user.AddReward(coins, xp);

            var newLevel = LevelCalculator.GetLevel(user.Experience);
            var leveledUp = newLevel > oldLevel;

            if (leveledUp)
            {
                await notifications.AddAsync(new Notification(
                    user.Id,
                    "LevelUp",
                    "Level Up!",
                    $"You're now Level {newLevel}!",
                    JsonSerializer.Serialize(new { newLevel })), ct);
            }

            var creditedEvent = new RewardCreditedIntegrationEvent
            {
                UserId = user.Id,
                CoinsAwarded = coins,
                ExperienceAwarded = xp,
                TotalCoins = user.Coins,
                TotalExperience = user.Experience,
                LeveledUp = leveledUp,
                NewLevel = newLevel,
            };

            var body = JsonSerializer.SerializeToUtf8Bytes(creditedEvent, (JsonSerializerOptions?)null);
            var props = new BasicProperties { Persistent = true, MessageId = Guid.NewGuid().ToString() };

            await using var publishChannel = await channel.CreateChannelAsync(cancellationToken: ct);
            await publishChannel.BasicPublishAsync(
                exchange: RaceEventsTopology.ExchangeName,
                routingKey: RaceEventsTopology.RewardCreditedRoutingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);
        }
    }
}