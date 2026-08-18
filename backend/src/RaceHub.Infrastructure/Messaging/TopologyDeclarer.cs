// RaceHub.Contracts/Messaging/TopologyDeclarer.cs
using RabbitMQ.Client;
using RaceHub.Contracts.Messaging;

namespace RaceHub.Infrastructure.Messaging;

public static class TopologyDeclarer
{
    public static async Task DeclareAsync(IChannel channel, CancellationToken ct = default)
    {
        await channel.ExchangeDeclareAsync(RaceEventsTopology.ExchangeName, RaceEventsTopology.ExchangeType, durable: true, cancellationToken: ct);

        foreach (var queue in new[] { RaceEventsTopology.Queues.Ranking, RaceEventsTopology.Queues.Reward, RaceEventsTopology.Queues.Statistics, RaceEventsTopology.Queues.Achievements })
        {
            var retryQueue = RaceEventsTopology.RetryQueueFor(queue);
            var dlq = RaceEventsTopology.DeadLetterQueueFor(queue);

            // Main queue: still declares x-dead-letter-exchange so
            // QueueDeclareAsync doesn't clash with the existing queue
            // (RabbitMQ rejects mismatched args). The consumer no longer
            // relies on this DLX for retries — retries are handled in
            // application code (IdempotentConsumer). The DLX remains as
            // a safety net for truly fatal errors that bypass the catch
            // block.
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = retryQueue,
                }, cancellationToken: ct);

            await channel.QueueBindAsync(queue, RaceEventsTopology.ExchangeName, RaceEventsTopology.RaceFinishedRoutingKey, cancellationToken: ct);

            // Retry queue: retained for compatibility with existing
            // queue declarations in RabbitMQ. The IdempotentConsumer now
            // handles retries in-process (exponential backoff) because
            // RabbitMQ 3.12+ drops messages that cycle through dead-
            // letter exchanges back to their origin queue (loop
            // prevention). This queue is effectively unused.
            await channel.QueueDeclareAsync(retryQueue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = 10000,
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = queue,
                }, cancellationToken: ct);

            // Terminal resting place once retries are exhausted (enforced
            // in consumer code). Nothing auto-consumes this; it's for
            // manual inspection/replay via the management UI.
            await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        }

        // reward-notify-queue: intentionally no retry/DLQ topology (see
        // its doc comment on RaceEventsTopology.Queues.RewardNotify) — a
        // plain durable queue bound to a routing key of its own, consumed
        // at-most-once by RaceHub.API's RewardNotificationRelayService.
        await channel.QueueDeclareAsync(RaceEventsTopology.Queues.RewardNotify, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(RaceEventsTopology.Queues.RewardNotify, RaceEventsTopology.ExchangeName, RaceEventsTopology.RewardCreditedRoutingKey, cancellationToken: ct);

        // achievement-notify-queue: same at-most-once relay pattern, for
        // badge-unlock toasts (consumed by RaceHub.API's
        // AchievementNotificationRelayService).
        await channel.QueueDeclareAsync(RaceEventsTopology.Queues.AchievementNotify, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(RaceEventsTopology.Queues.AchievementNotify, RaceEventsTopology.ExchangeName, RaceEventsTopology.AchievementUnlockedRoutingKey, cancellationToken: ct);
    }
}