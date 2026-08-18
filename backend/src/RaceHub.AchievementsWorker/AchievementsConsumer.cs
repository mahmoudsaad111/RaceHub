using System.Text.Json;
using RabbitMQ.Client;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Contracts.Achievements;
using RaceHub.Contracts.Events;
using RaceHub.Contracts.Messaging;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Messaging;

namespace RaceHub.AchievementsWorker;

/// <summary>
/// The fourth independent consumer of race.finished. Nothing about
/// Ranking/Reward/Statistics changed to add it — that's the payoff of the
/// topic-exchange design: new consumer = new queue + binding + project.
///
/// For each finisher it evaluates the one-time achievement catalog against
/// RaceHistoryEntry (StatisticsWorker's read model) and, on an unlock,
/// writes the UserAchievement row + a Notification and publishes
/// achievement.unlocked for the live SignalR toast.
///
/// It runs CONCURRENTLY with StatisticsConsumer — whether this race's own
/// history rows have landed when we read is a race. Every query below
/// therefore either explicitly excludes this race (the *ExcludingRace
/// methods) or is filtered for it in code, and the current race's result
/// is merged in manually, so counts are correct both ways.
/// </summary>
public class AchievementsConsumer : IdempotentConsumer
{
    private const int PodiumPosition = 3;
    private const int PodiumStreakLength = 3;
    private const int MaxPriorRacesFetched = 50;

    public AchievementsConsumer(IConnection connection, IServiceScopeFactory scopeFactory, ILogger<AchievementsConsumer> logger)
        : base(connection, scopeFactory, logger, RaceEventsTopology.Queues.Achievements, nameof(AchievementsConsumer)) { }

    protected override async Task HandleAsync(string routingKey, string payload, IServiceProvider scopeProvider, CancellationToken ct)
    {
        if (routingKey != RaceEventsTopology.RaceFinishedRoutingKey) return;

        var evt = JsonSerializer.Deserialize<RaceFinishedIntegrationEvent>(payload)
            ?? throw new InvalidOperationException("Malformed RaceFinished payload");

        var history = scopeProvider.GetRequiredService<IRaceHistoryRepository>();
        var achievements = scopeProvider.GetRequiredService<IUserAchievementRepository>();
        var notifications = scopeProvider.GetRequiredService<INotificationRepository>();
        var tracks = scopeProvider.GetRequiredService<ITrackRepository>();
        var connection = scopeProvider.GetRequiredService<IConnection>();

        var trackNames = await tracks.GetNamesByIdsAsync([evt.TrackId], ct);
        var trackName = trackNames.GetValueOrDefault(evt.TrackId, "Unknown Track");

        foreach (var result in evt.Results)
        {
            var unlockedKeys = (await achievements.GetAllForUserAsync(result.UserId, ct))
                .Select(a => a.Key)
                .ToHashSet();

            // Prior races, newest first, with this race's own row filtered
            // out if StatisticsConsumer happened to land it first.
            var priorRaces = (await history.GetRecentByUserIdAsync(result.UserId, MaxPriorRacesFetched, ct))
                .Where(e => e.RaceId != evt.RaceId)
                .ToList();

            var totalRacesIncludingThis = priorRaces.Count + 1;
            var winsIncludingThis = await history.GetWinCountExcludingRaceAsync(result.UserId, evt.RaceId, ct)
                + (result.Position == 1 ? 1 : 0);

            var toUnlock = new List<AchievementDefinition>();

            TryUnlock(toUnlock, unlockedKeys, AchievementDefinitions.FirstRace);
            if (totalRacesIncludingThis >= 10) TryUnlock(toUnlock, unlockedKeys, AchievementDefinitions.Races10);
            if (totalRacesIncludingThis >= 50) TryUnlock(toUnlock, unlockedKeys, AchievementDefinitions.Races50);
            if (result.Position == 1) TryUnlock(toUnlock, unlockedKeys, AchievementDefinitions.FirstWin);
            if (winsIncludingThis >= 10) TryUnlock(toUnlock, unlockedKeys, AchievementDefinitions.Wins10);
            if (IsPodiumStreak(result, priorRaces)) TryUnlock(toUnlock, unlockedKeys, AchievementDefinitions.PodiumStreak3);

            foreach (var definition in toUnlock)
            {
                await achievements.AddAsync(new UserAchievement(result.UserId, definition.Key), ct);

                await notifications.AddAsync(new Notification(
                    result.UserId,
                    "AchievementUnlocked",
                    $"Achievement unlocked: {definition.Title}",
                    definition.Description), ct);

                await PublishUnlockedAsync(connection, result.UserId, definition, ct);
            }

            // Personal best: recurring notification, NOT a one-time badge
            // (see UserAchievement's doc comment). First finish on a track
            // has nothing to beat, so it's not a "best" yet.
            var bestBefore = await history.GetBestTimeForTrackExcludingRaceAsync(result.UserId, evt.TrackId, evt.RaceId, ct);

            if (bestBefore is int previousBest && result.FinishTimeMs < previousBest)
            {
                await notifications.AddAsync(new Notification(
                    result.UserId,
                    "PersonalBest",
                    "New personal best!",
                    $"You beat your best time on {trackName}: {FormatMs(previousBest)} → {FormatMs(result.FinishTimeMs)}."), ct);
            }
        }
    }

    private static void TryUnlock(List<AchievementDefinition> toUnlock, HashSet<string> alreadyUnlocked, string key)
    {
        if (alreadyUnlocked.Contains(key)) return;

        var definition = AchievementDefinitions.Find(key);
        if (definition is not null) toUnlock.Add(definition);
    }

    /// <summary>
    /// The streak counts backward from this race: this finish plus the two
    /// prior races must all be podiums. priorRaces is newest-first; if this
    /// race's row is already among them it was filtered out above, so
    /// index 0 is genuinely the race before this one. The count check
    /// matters: All() is vacuously true on an empty list, which would
    /// otherwise hand out a "3 in a row" badge after a single podium.
    /// </summary>
    private static bool IsPodiumStreak(RaceFinishedIntegrationEvent.PlayerResult result, List<RaceHistoryEntry> priorRaces)
    {
        if (result.Position > PodiumPosition) return false;

        var previousTwo = priorRaces.Take(PodiumStreakLength - 1).ToList();

        return previousTwo.Count == PodiumStreakLength - 1
            && previousTwo.All(e => e.Position <= PodiumPosition);
    }

    // Best-effort publish for the live toast — same deliberate non-outbox
    // pattern as RewardWorker's reward.credited publish. The
    // UserAchievement row written above is the durable record; if this
    // publish or the API's relay drops it, the badge still shows on the
    // next profile load, only the live toast is lost.
    private static async Task PublishUnlockedAsync(
        IConnection connection, Guid userId, AchievementDefinition definition, CancellationToken ct)
    {
        var unlockedEvent = new AchievementUnlockedIntegrationEvent
        {
            UserId = userId,
            AchievementKey = definition.Key,
            Title = definition.Title,
            Description = definition.Description,
        };

        var body = JsonSerializer.SerializeToUtf8Bytes(unlockedEvent, (JsonSerializerOptions?)null);
        var props = new BasicProperties { Persistent = true, MessageId = Guid.NewGuid().ToString() };

        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.BasicPublishAsync(
            exchange: RaceEventsTopology.ExchangeName,
            routingKey: RaceEventsTopology.AchievementUnlockedRoutingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);
    }

    private static string FormatMs(int milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds).ToString(@"mm\:ss\.fff");
}
