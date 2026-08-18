// RaceHub.Domain/Entities/PlayerStatistics.cs
namespace RaceHub.Domain.Entities;

public class PlayerStatistics
{
    private const int DefaultRating = 1000;
    private const int KFactor = 32;
    private const int MinimumRating = 100;

    public Guid UserId { get; private set; }
    public int TotalRaces { get; private set; }
    public int TotalWins { get; private set; }
    public int? BestTimeMs { get; private set; }
    public int RatingPoints { get; private set; } = DefaultRating;

    private PlayerStatistics() { }
    public PlayerStatistics(Guid userId) => UserId = userId;

    /// <summary>
    /// fieldAverageRating is a snapshot of every participant's rating
    /// *before* this race's results are applied to any of them — see
    /// RankingConsumer, which computes it once across the whole field
    /// before calling this for each player, so the field average isn't
    /// skewed by whichever player happens to be processed first.
    /// </summary>
    public void RecordRaceResult(int position, int finishTimeMs, double fieldAverageRating, int fieldSize)
    {
        TotalRaces++;
        if (position == 1) TotalWins++;
        if (BestTimeMs is null || finishTimeMs < BestTimeMs) BestTimeMs = finishTimeMs;

        ApplyRatingChange(position, fieldAverageRating, fieldSize);
    }

    /// <summary>
    /// Lightweight Elo-style adjustment: this player's rating moves based
    /// on how they finished relative to what a player of their current
    /// rating would be "expected" to do against this field's average
    /// rating — the same expected-score formula chess Elo uses (1 / (1 +
    /// 10^((opponentRating - selfRating) / 400))), just fed a field
    /// average instead of a single opponent's rating.
    ///
    /// "Expected" position is converted to a 0-1 actual score the same
    /// way: 1st place scores 1.0, last place scores 0.0, evenly spaced in
    /// between. Beating a field rated higher than you nets more points
    /// than the same finish against a weaker field; finishing worse than
    /// your rating would predict costs points even off a win in a very
    /// weak field, since "expected" for a strong player in a weak field is
    /// already close to 1.0.
    /// </summary>
    private void ApplyRatingChange(int position, double fieldAverageRating, int fieldSize)
    {
        if (fieldSize <= 1)
        {
            // Nothing to rate a solo race against — leave rating untouched.
            return;
        }

        var expected = 1.0 / (1.0 + Math.Pow(10, (fieldAverageRating - RatingPoints) / 400.0));
        var actual = (double)(fieldSize - position) / (fieldSize - 1);

        var delta = (int)Math.Round(KFactor * (actual - expected));

        RatingPoints = Math.Max(MinimumRating, RatingPoints + delta);
    }
}
