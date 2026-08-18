namespace RaceHub.Application.Interfaces;

/// <summary>
/// Tracks which users currently have at least one live SignalR connection.
/// Lives behind an interface so RaceHub (API layer, owns the actual
/// connect/disconnect events) and GetFriendsQueryHandler (Application
/// layer, needs to read online status for the friends list) can share one
/// source of truth without the Application layer depending on SignalR.
///
/// In-memory only — fine for a single API instance. If RaceHub.API is ever
/// scaled to multiple instances, this needs to move to Redis (see
/// RaceHub.Infrastructure/Redis) so presence is consistent across pods.
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Registers a new connection. Returns true if this was the user's first (0 -> 1) — i.e. they just came online.</summary>
    bool AddConnection(Guid userId);

    /// <summary>Removes a connection. Returns true if this was the user's last (1 -> 0) — i.e. they just went offline.</summary>
    bool RemoveConnection(Guid userId);

    bool IsOnline(Guid userId);
}
