using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Persistence;

public interface IRaceRepository
{
    Task AddAsync(Race race, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a player created for an already-tracked race as Added. This is
    /// explicit because relationship change detection can otherwise infer an
    /// update when the player arrives with a client-generated Guid key.
    /// </summary>
    void AddPlayer(RacePlayer player);

    /// <summary>
    /// Tracked, with Players/Track/Car/User included - every consumer
    /// (join/leave/ready/start, and the room-detail query) needs the full
    /// graph either to mutate it or to project a RaceDetailDto from it.
    /// </summary>
    Task<Race?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only snapshot with the full graph loaded. Use this after a
    /// mutation when the caller needs the latest persisted state.
    /// </summary>
    Task<Race?> GetSnapshotByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Rooms still accepting players, for the lobby list.</summary>
    Task<IReadOnlyList<Race>> GetOpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every non-finished race (Waiting/Starting/Running) that has at least
    /// one of the given users as a player - used by GetFriendsQueryHandler
    /// to show "friend is in a room, join them" on the friends list.
    /// AsNoTracking since this is read-only.
    /// </summary>
    Task<IReadOnlyList<Race>> GetActiveRacesForUsersAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every still-Waiting (not yet started) race this user currently
    /// occupies, tracked and with the full Players/Track/Car graph loaded
    /// so the caller can mutate and save it — used by RaceHub on disconnect
    /// to remove a player from any room(s) they never explicitly left. A
    /// list rather than a single race because nothing stops a user from
    /// having joined more than one waiting room simultaneously.
    /// </summary>
    Task<IReadOnlyList<Race>> GetWaitingRacesForUserAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every Starting/Running race this user currently occupies, tracked
    /// with the full graph loaded — used by RaceHub on disconnect to mark
    /// the player Disconnected (never removed, unlike the Waiting-race
    /// case above, since removing would cascade-delete their Lap history)
    /// so an abandoned mid-race player doesn't leave the race stuck at
    /// Running forever for everyone still in it.
    /// </summary>
    Task<IReadOnlyList<Race>> GetInProgressRacesForUserAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Used to clean up a room once its last player leaves.</summary>
    void Remove(Race race);
}
