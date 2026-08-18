using MediatR;
using RaceHub.Application.Common;

namespace RaceHub.Application.Features.Races.FinishRace;

/// <summary>Sent by RaceHub.ReportFinished — the client tells the server it crossed the final line.</summary>
public record FinishPlayerCommand(
    Guid RaceId,
    Guid UserId,
    int TotalTimeMs) : IRequest<Result<FinishPlayerResult>>;
