using RaceHub.Application.DTOs.Races;
using RaceHub.Domain.Entities;

namespace RaceHub.Application.Features.Races.Common;

/// <summary>
/// Race entity -> DTO projection shared by every Race feature handler
/// (Create/Join/Leave/Ready/Start/GetById all return the same room shape)
/// and by RacesController when it broadcasts room state over SignalR.
/// Assumes race.Track and race.Players[].User/.Car are loaded — see
/// IRaceRepository.GetByIdAsync.
/// </summary>
public static class RaceMapper
{
    public static RaceDetailDto ToDetailDto(Race race)
    {
        var players = race.Players
            .Select(p => new RacePlayerDto(
                p.UserId,
                p.User.DisplayName,
                p.CarId,
                p.Car.Name,
                p.Status.ToString(),
                p.UserId == race.HostUserId))
            .ToList();

        return new RaceDetailDto(
            race.Id,
            race.TrackId,
            race.Track.Name,
            race.TotalLaps,
            race.HostUserId,
            race.Status.ToString(),
            race.MaxPlayers,
            players);
    }
}
