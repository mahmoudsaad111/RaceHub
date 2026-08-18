using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Persistence;

public interface ITrackRepository
{
    Task<Track?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Track>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Track name lookup for a batch of IDs, keyed by TrackId. Used to enrich read models (like RaceHistoryEntry) that store TrackId but not a name.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
