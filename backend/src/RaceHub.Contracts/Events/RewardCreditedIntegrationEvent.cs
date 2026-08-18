// RaceHub.Contracts/RewardCreditedIntegrationEvent.cs
namespace RaceHub.Contracts.Events;

/// <summary>
/// Published by RewardWorker (not via the transactional outbox — this is
/// a best-effort real-time nicety, not the durable source of truth; the
/// User.Coins/Experience row it was derived from already committed before
/// this publish happens) purely so RaceHub.API can relay a live SignalR
/// push to the affected user. Deliberately separate from
/// RaceFinishedIntegrationEvent: that one is "a race ended, here's who
/// placed where" (ranking/statistics/reward all care); this one is "a
/// specific user's balance just changed" (only that one connected client
/// cares, and only for a UI toast — nothing rebuilds state from it).
/// </summary>
public sealed record RewardCreditedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required int CoinsAwarded { get; init; }
    public required int ExperienceAwarded { get; init; }
    public required int TotalCoins { get; init; }
    public required int TotalExperience { get; init; }
    public required bool LeveledUp { get; init; }
    public required int NewLevel { get; init; }
}
