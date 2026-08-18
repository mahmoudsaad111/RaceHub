namespace RaceHub.Application.DTOs.Users;

public record RecentRaceDto(
    Guid RaceId,
    string TrackName,
    int FinishingPosition,
    TimeSpan TotalRaceTime,
    DateTime CreatedAtUtc);

/// <summary>
/// Profile screen payload: identity fields straight off the User/Identity
/// record, plus race stats aggregated from RaceResult. Level/XP-into-level
/// are derived from the cumulative Experience counter using a flat
/// XP-per-level curve (see GetProfileQueryHandler) — there's no separate
/// "Level" column on User, it's computed on read so the curve can change
/// later without a migration.
/// </summary>
public record ProfileDto(
    Guid UserId,
    string Email,
    string DisplayName,
    int Experience,
    int Coins,
    int Level,
    int XpIntoLevel,
    int XpForNextLevel,
    int TotalRaces,
    int Wins,
    TimeSpan? BestLapTime,
    IReadOnlyList<RecentRaceDto> RecentRaces,
    int RatingPoints = 0);
