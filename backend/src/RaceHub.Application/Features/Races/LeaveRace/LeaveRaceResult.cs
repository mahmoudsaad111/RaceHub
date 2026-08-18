using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.LeaveRace;

/// <summary>
/// RaceDetail is null when the room was deleted (its last player just left)
/// — the controller uses that to broadcast "RoomClosed" instead of
/// "PlayerLeft" over SignalR.
/// </summary>
public record LeaveRaceResult(bool RoomClosed, RaceDetailDto? RaceDetail);
