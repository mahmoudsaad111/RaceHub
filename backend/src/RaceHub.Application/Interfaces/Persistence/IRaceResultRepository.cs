using RaceHub.Domain.Entities;

namespace RaceHub.Application.Interfaces.Persistence;

/// <summary>
/// Read-side aggregate stats for a user's race history. Kept separate from
/// a generic "IRaceResultRepository CRUD" interface since every current
/// consumer (the profile screen) only ever needs the aggregate, not
/// individual RaceResult entities.
/// </summary>
public interface IRaceResultRepository
{
    Task AddAsync(RaceResult raceResult, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RaceResult>> GetByRaceIdAsync(Guid raceId, CancellationToken cancellationToken = default);

    Task<int> GetTotalRacesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> GetWinsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<TimeSpan?> GetBestLapTimeAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RaceResult>> GetRecentAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default);
}
