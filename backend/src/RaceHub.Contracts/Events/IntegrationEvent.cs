// RaceHub.Contracts/IntegrationEvent.cs
namespace RaceHub.Contracts.Events;

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public int EventVersion { get; init; } = 1;
}