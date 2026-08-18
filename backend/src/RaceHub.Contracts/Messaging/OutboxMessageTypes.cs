// RaceHub.Contracts/Messaging/OutboxMessageTypes.cs
namespace RaceHub.Contracts.Messaging;

public static class OutboxMessageTypes
{
    public const string RaceFinished = "RaceFinished";

    public static string RoutingKeyFor(string type) => type switch
    {
        RaceFinished => RaceEventsTopology.RaceFinishedRoutingKey,
        _ => throw new InvalidOperationException($"No routing key mapped for outbox type '{type}'"),
    };
}