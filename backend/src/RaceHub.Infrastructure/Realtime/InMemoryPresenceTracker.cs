using System.Collections.Concurrent;
using RaceHub.Application.Interfaces;

namespace RaceHub.Infrastructure.Realtime;

/// <summary>
/// Counts open connections per user (a user can have the app open in two
/// tabs/devices) so online/offline only fires on the 0-&gt;1 and 1-&gt;0
/// transitions. Registered as a singleton — see IPresenceTracker for why
/// this lives behind an interface instead of RaceHub owning its own
/// private dictionary (which is what it did before this fix).
/// </summary>
public class InMemoryPresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connectionCounts = new();

    public bool AddConnection(Guid userId)
    {
        var newCount = _connectionCounts.AddOrUpdate(userId, 1, (_, count) => count + 1);
        return newCount == 1;
    }

    public bool RemoveConnection(Guid userId)
    {
        var newCount = _connectionCounts.AddOrUpdate(userId, 0, (_, count) => Math.Max(0, count - 1));

        if (newCount == 0)
        {
            _connectionCounts.TryRemove(userId, out _);
            return true;
        }

        return false;
    }

    public bool IsOnline(Guid userId) =>
        _connectionCounts.TryGetValue(userId, out var count) && count > 0;
}
