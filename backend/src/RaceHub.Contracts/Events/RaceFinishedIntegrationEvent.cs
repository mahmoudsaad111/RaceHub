// RaceHub.Contracts/RaceFinishedIntegrationEvent.cs
namespace RaceHub.Contracts.Events;

public sealed record RaceFinishedIntegrationEvent : IntegrationEvent
{
    public required Guid RaceId { get; init; }
    public required Guid TrackId { get; init; }
    public required IReadOnlyList<PlayerResult> Results { get; init; }

    public sealed record PlayerResult(Guid UserId, int Position, int FinishTimeMs);
}