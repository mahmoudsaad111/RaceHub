using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;

namespace RaceHub.Infrastructure.Persistence.Repositories;

/// <summary>
/// Backs the Inbox pattern (see IdempotentConsumer): the composite key is
/// (MessageId, ConsumerName), not MessageId alone — the same
/// RaceFinishedIntegrationEvent is legitimately delivered to three
/// independent queues (ranking/reward/statistics), and each consumer needs
/// its own independent "have I processed this" record.
/// </summary>
public class ProcessedMessageRepository : IProcessedMessageRepository
{
    private readonly AppDbContext _context;

    public ProcessedMessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<bool> HasBeenProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default)
    {
        return _context.Set<ProcessedMessage>()
            .AsNoTracking()
            .AnyAsync(m => m.MessageId == messageId && m.ConsumerName == consumerName, ct);
    }

    public async Task MarkProcessedAsync(Guid messageId, string consumerName, CancellationToken ct = default)
    {
        // Note: deliberately does NOT call SaveChangesAsync here — the whole
        // point of the Inbox pattern is that this marker commits in the
        // SAME SaveChangesAsync call as the consumer's own domain writes
        // (see IdempotentConsumer.ExecuteAsync), so a crash between the two
        // can never leave one committed without the other.
        await _context.Set<ProcessedMessage>().AddAsync(
            new ProcessedMessage(messageId, consumerName, DateTime.UtcNow), ct);
    }
}
