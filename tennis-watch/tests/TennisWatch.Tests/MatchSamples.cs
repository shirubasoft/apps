using TennisWatch.Core.Matches;
using TennisWatch.Core.Scoring;

namespace TennisWatch.Tests;

internal static class MatchSamples
{
    public static readonly DateTimeOffset StartedAt = new(2026, 9, 12, 15, 30, 0, TimeSpan.FromHours(-3));

    public static MatchBook ThreeMatches()
    {
        var book = new MatchBook(StartedAt);
        book.Award(Side.Left);
        book.NextMatch(StartedAt.AddDays(1));
        book.Award(Side.Right);
        book.NextMatch(StartedAt.AddDays(2));
        book.Award(Side.Left);
        book.Award(Side.Left);
        return book;
    }
}
