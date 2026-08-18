using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Races;

namespace RaceHub.Application.Features.Races.AcceptInvite;

public record AcceptRaceInviteCommand(Guid RaceId, Guid UserId) : IRequest<Result<RaceDetailDto>>;
