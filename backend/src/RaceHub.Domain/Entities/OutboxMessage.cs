// RaceHub.Domain/Entities/OutboxMessage.cs
using System.Text.Json;

namespace RaceHub.Domain.Entities;

public class OutboxMessage : BaseEntity
{
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!; // JSON
    public DateTime OccurredOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string type, object integrationEvent)
    {
        return new OutboxMessage
        {
            Type = type,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredOnUtc = DateTime.UtcNow,
        };
    }

    public void MarkProcessed() => ProcessedOnUtc = DateTime.UtcNow;

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error;
    }
}