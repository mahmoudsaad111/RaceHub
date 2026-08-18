// RaceHub.Contracts/Messaging/RaceEventsTopology.cs
namespace RaceHub.Contracts.Messaging;
/// <summary>
/// Every string here is referenced by both the publisher and every
/// consumer. Centralizing them is what prevents the classic bug: a typo in
/// a routing key on one side means a binding silently never matches, and
/// events vanish with no error anywhere.
/// </summary>
public static class RaceEventsTopology
{
    public const string ExchangeName = "race.events";
    public const string ExchangeType = "topic";
    public const string RaceFinishedRoutingKey = "race.finished";

    /// <summary>
    /// Published by RewardWorker straight to RaceHub.API (see
    /// RewardCreditedIntegrationEvent) so the affected user's client can
    /// get a live "+150 coins" / "Level up!" toast the moment the reward
    /// actually lands, instead of only finding out next time they poll
    /// their profile.
    /// </summary>
    public const string RewardCreditedRoutingKey = "reward.credited";

    /// <summary>
    /// Published by AchievementsWorker when a badge unlocks (see
    /// AchievementUnlockedIntegrationEvent) — same best-effort realtime
    /// toast pattern as reward.credited: the UserAchievement row is the
    /// durable record, this exists so the toast pops live.
    /// </summary>
    public const string AchievementUnlockedRoutingKey = "achievement.unlocked";

    public static class Queues
    {
        public const string Ranking = "ranking-queue";
        public const string Reward = "reward-queue";
        public const string Statistics = "statistics-queue";

        /// <summary>
        /// Fourth independent consumer of race.finished. Added long after
        /// the original three without touching any of them — the concrete
        /// payoff of choosing a topic exchange: new consumer = new queue +
        /// new binding, zero changes to publisher or existing consumers.
        /// </summary>
        public const string Achievements = "achievements-queue";

        /// <summary>
        /// Deliberately NOT in TopologyDeclarer's retry/DLQ loop like the
        /// business-critical queues above — this is a best-effort,
        /// at-most-once realtime notification, not business-critical data
        /// (the actual Coins/Experience update already committed before
        /// this was published). If RaceHub.API is briefly down when this
        /// arrives, dropping it just means the client's toast doesn't
        /// appear; their next profile load still shows the correct,
        /// already-persisted balance regardless.
        /// </summary>
        public const string RewardNotify = "reward-notify-queue";

        /// <summary>Same at-most-once relay pattern as RewardNotify, for achievement toasts.</summary>
        public const string AchievementNotify = "achievement-notify-queue";
    }

    public static string RetryQueueFor(string queueName) => $"{queueName}.retry";
    public static string DeadLetterQueueFor(string queueName) => $"{queueName}.dlq";
}
