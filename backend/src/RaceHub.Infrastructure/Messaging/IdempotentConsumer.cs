// RaceHub.Infrastructure/Messaging/IdempotentConsumer.cs
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Contracts.Messaging;
using RaceHub.Infrastructure.Messaging;

namespace RaceHub.Infrastructure.Messaging;

public abstract class IdempotentConsumer : BackgroundService
{
    private const int MaxAttempts = 5;

    private readonly IConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly string _queueName;
    private readonly string _consumerName;

    protected IdempotentConsumer(IConnection connection, IServiceScopeFactory scopeFactory, ILogger logger, string queueName, string consumerName)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queueName = queueName;
        _consumerName = consumerName;
    }

    /// Resolve whatever repositories you need from `scopeProvider` — it's
    /// scoped per-message and shares one DbContext/transaction with the
    /// idempotency marker this base class writes. Add your changes to
    /// those repositories but do NOT call SaveChanges/CommitAsync
    /// yourself — the base class does that once, after this returns,
    /// atomically together with the processed-marker.
    protected abstract Task HandleAsync(string routingKey, string payload, IServiceProvider scopeProvider, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await TopologyDeclarer.DeclareAsync(channel, stoppingToken); // idempotent — safe even if the API already declared it
        await channel.BasicQosAsync(0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) => await OnMessageAsync(channel, ea, stoppingToken);

        await channel.BasicConsumeAsync(_queueName, autoAck: false, consumer, cancellationToken: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task OnMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var messageId = Guid.Parse(ea.BasicProperties.MessageId!);
        var payload = Encoding.UTF8.GetString(ea.Body.Span);

        // Idempotency check — already processed means we can ack and move on
        using (var checkScope = _scopeFactory.CreateScope())
        {
            var checkRepo = checkScope.ServiceProvider.GetRequiredService<IProcessedMessageRepository>();
            if (await checkRepo.HasBeenProcessedAsync(messageId, _consumerName, stoppingToken))
            {
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                return;
            }
        }

        // Application-level retry loop with exponential backoff.
        // RabbitMQ 3.12+ drops messages that cycle through dead-letter
        // exchanges back to their origin queue (loop prevention), so we
        // can't rely on the DLX retry topology — retries must be
        // managed in code.
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var scope = _scopeFactory.CreateScope();
            var processedMessages = scope.ServiceProvider.GetRequiredService<IProcessedMessageRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                await HandleAsync(ea.RoutingKey, payload, scope.ServiceProvider, stoppingToken);
                await processedMessages.MarkProcessedAsync(messageId, _consumerName, stoppingToken);
                await unitOfWork.SaveChangesAsync(stoppingToken);
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s, 8s
                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxAttempts} failed for {MessageId}. Retrying in {DelaySeconds}s",
                    attempt, MaxAttempts, messageId, (int)delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "All {MaxAttempts} attempts exhausted for {MessageId}. Publishing to DLQ.",
                    MaxAttempts, messageId);
                await channel.BasicPublishAsync(
                    "", RaceEventsTopology.DeadLetterQueueFor(_queueName),
                    ea.Body, cancellationToken: stoppingToken);
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
        }
    }
}
