namespace RaceHub.Contracts.Rewards;

/// <summary>
/// Single source of truth for coin/XP rewards by finishing position.
/// Referenced by both RaceHub.API (FinishPlayerCommandHandler, for the
/// immediate RaceResult.CoinsEarned/ExperienceEarned display snapshot) and
/// RaceHub.RewardWorker (RewardConsumer, for the actual async User.Coins/
/// Experience credit) — previously duplicated verbatim in both places,
/// which meant tuning the curve in one spot without the other would let
/// the displayed reward and the actually-credited reward silently drift
/// apart.
/// </summary>
public static class RewardCurve
{
    public static (int Coins, int Xp) ForPosition(int position) => position switch
    {
        1 => (150, 150),
        2 => (100, 100),
        3 => (75, 75),
        4 => (50, 50),
        _ => (25, 25),
    };
}
