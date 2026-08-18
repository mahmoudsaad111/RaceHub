using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Achievements;

namespace RaceHub.Application.Features.Achievements.GetAchievements;

public record GetAchievementsQuery(Guid UserId)
    : IRequest<Result<IReadOnlyList<AchievementDto>>>;
