namespace TennisWatch.Core.Scoring;

public readonly record struct ScorePair
{
    public int Left { get; init; }
    public int Right { get; init; }

    public ScorePair Award(Side side) => side switch
    {
        Side.Left => this with { Left = Left + 1 },
        Side.Right => this with { Right = Right + 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(side))
    };

    public bool HasWinner(int minimum) => Math.Max(Left, Right) >= minimum && Math.Abs(Left - Right) >= 2;

    public override string ToString() => $"{Left} × {Right}";
}
