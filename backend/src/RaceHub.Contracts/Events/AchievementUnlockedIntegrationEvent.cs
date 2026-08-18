namespace RaceHub.Contracts.Events;

/// <summary>
/// Published by AchievementsWorker (best-effort, straight to the exchange
/// — the UserAchievement row and its Notification already committed in
/// the same transaction that processed the race.finished event, so this
/// is purely the realtime toast path, same pattern as
/// RewardCreditedIntegrationEvent). RaceHub.API relays it to the affected
/// user over SignalR so the badge unlock pops the moment it happens
/// instead of on their next profile visit.
/// </summary>
public sealed record AchievementUnlockedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }
    public required string AchievementKey { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
}
