using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.CreateRace;

/// <summary>Creates a room and immediately joins the host into it with their chosen car.</summary>
public record CreateRaceCommand(
    Guid HostUserId,
    Guid TrackId,
    Guid CarId,
    int MaxPlayers) : IRequest<Result<RaceDetailDto>>;
