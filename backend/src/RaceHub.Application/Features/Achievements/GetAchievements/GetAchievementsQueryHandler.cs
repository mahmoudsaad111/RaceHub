using MediatR;
using RaceHub.Application.Common;
using RaceHub.Application.DTOs.Achievements;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Contracts.Achievements;

namespace RaceHub.Application.Features.Achievements.GetAchievements;

public class GetAchievementsQueryHandler
    : IRequestHandler<GetAchievementsQuery, Result<IReadOnlyList<AchievementDto>>>
{
    private readonly IUserAchievementRepository _userAchievements;

    public GetAchievementsQueryHandler(IUserAchievementRepository userAchievements)
    {
        _userAchievements = userAchievements;
    }

    public async Task<Result<IReadOnlyList<AchievementDto>>> Handle(
        GetAchievementsQuery request,
        CancellationToken cancellationToken)
    {
        var unlocked = await _userAchievements.GetAllForUserAsync(request.UserId, cancellationToken);

        var unlockedByKey = unlocked.ToDictionary(a => a.Key, a => a.UnlockedAtUtc);

        var achievements = AchievementDefinitions.All
            .Select(def => new AchievementDto(
                def.Key,
                def.Title,
                def.Description,
                unlockedByKey.ContainsKey(def.Key),
                unlockedByKey.GetValueOrDefault(def.Key)))
            .ToList();

        return Result<IReadOnlyList<AchievementDto>>.Success(achievements);
    }
}
