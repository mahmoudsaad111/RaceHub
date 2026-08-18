namespace RaceHub.Contracts.Ranking;

/// <summary>
/// Simple fixed points-by-position table (same shape as F1's scoring),
/// scaled down for smaller lobbies so winning a 2-car race isn't worth the
/// same rating as winning an 8-car one. Deliberately not a pairwise ELO —
/// ELO needs each opponent's own rating to compute a matchup-specific
/// delta, which is more to reason about (and demo) for marginal accuracy
/// gain over a fixed table at this scale. Single source of truth,
/// referenced only by RaceHub.RankingWorker today, but living in Contracts
/// since it's conceptually part of the race.finished event's meaning, not
/// an implementation detail of one consumer.
/// </summary>
public static class RankingPointsTable
{
    private static readonly int[] BasePointsByPosition = [25, 18, 15, 12, 10, 8, 6, 4];
    private const int ParticipationPoints = 1;
    private const int ReferenceFieldSize = 8;

    public static int ForPosition(int position, int fieldSize)
    {
        var basePoints = position >= 1 && position <= BasePointsByPosition.Length
            ? BasePointsByPosition[position - 1]
            : ParticipationPoints;

        var scale = Math.Min(1.0, fieldSize / (double)ReferenceFieldSize);

        return Math.Max(ParticipationPoints, (int)Math.Round(basePoints * scale));
    }
}
