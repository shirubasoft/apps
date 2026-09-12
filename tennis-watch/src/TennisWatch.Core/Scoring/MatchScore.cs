using System.Collections.Immutable;
using System.Globalization;

namespace TennisWatch.Core.Scoring;

public sealed record MatchScore
{
    public ScorePair Points { get; init; }
    public ScorePair Games { get; init; }
    public ImmutableArray<ScorePair> CompletedSets { get; init; } = [];
    public bool IsTiebreak => Games is { Left: 6, Right: 6 };

    public string PointText(Side side)
    {
        var points = side == Side.Left ? Points.Left : Points.Right;
        if (IsTiebreak) return points.ToString(CultureInfo.InvariantCulture);
        return points switch { 0 => "0", 1 => "15", 2 => "30", 3 => "40", _ => "AD" };
    }

    public MatchScore Award(Side side)
    {
        var points = Points.Award(side);
        if (!points.HasWinner(IsTiebreak ? 7 : 4))
        {
            if (!IsTiebreak && points is { Left: >= 4, Right: >= 4 })
                points = new ScorePair { Left = 3, Right = 3 };
            return this with { Points = points };
        }

        var games = Games.Award(side);
        return IsTiebreak || games.HasWinner(6)
            ? new MatchScore { CompletedSets = CompletedSets.Add(games) }
            : this with { Points = default, Games = games };
    }

    public static MatchScore Replay(IEnumerable<Side> points) =>
        points.Aggregate(new MatchScore(), (score, side) => score.Award(side));
}
