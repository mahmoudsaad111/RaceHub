using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.RecordLap;

/// <summary>Sent by RaceHub.ReportLapCompleted — the client tells the server it crossed the line.</summary>
public record RecordLapCommand(
    Guid RaceId,
    Guid UserId,
    int LapNumber,
    int LapTimeMs) : IRequest<Result<PlayerLapDto>>;
