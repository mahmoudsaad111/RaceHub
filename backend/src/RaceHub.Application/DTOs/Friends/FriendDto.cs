namespace RaceHub.Application.DTOs.Friends;

/// <summary>Room a friend is currently sitting in, if any — lets the friends list show a "Join" button.</summary>
public record FriendCurrentRaceDto(
    Guid RaceId,
    string TrackName,
    int CurrentPlayers,
    int MaxPlayers,
    /// <summary>"Waiting" | "Starting" | "Running" — Finished races are never surfaced here.</summary>
    string Status);

public record FriendDto(
    Guid UserId,
    string DisplayName,
    bool IsOnline,
    FriendCurrentRaceDto? CurrentRace);

public record PendingFriendRequestDto(
    Guid FriendshipId,
    Guid RequesterId,
    string RequesterDisplayName,
    DateTime CreatedAtUtc);
