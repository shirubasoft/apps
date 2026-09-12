using TennisWatch.Core.Matches;
using TennisWatch.Core.Scoring;

namespace TennisWatch.Tests;

public sealed class MatchBookTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(24)]
    public void Undo_restores_point_game_and_set_boundaries(int points)
    {
        var book = new MatchBook();
        for (var i = 0; i < points; i++) book.Award(Side.Left);
        book.Undo();
        var expected = MatchScore.Replay(Enumerable.Repeat(Side.Left, Math.Max(0, points - 1)));
        Assert.Equal(expected.Points, book.Score.Points);
        Assert.Equal(expected.Games, book.Score.Games);
        Assert.Equal(expected.CompletedSets.ToArray(), book.Score.CompletedSets.ToArray());
        Assert.Equal(points > 1, book.CanUndo);
    }

    [Theory]
    [InlineData("LLLRRRL")]
    [InlineData("LLLRRRLR")]
    [InlineData("LLLRRRLL")]
    public void Undo_preserves_deuce_and_advantage(string points)
    {
        var book = new MatchBook();
        foreach (var side in ScoringTests.Sides(points)) book.Award(side);
        book.Undo();
        var expected = MatchScore.Replay(ScoringTests.Sides(points[..^1]));
        Assert.Equal(expected.Points, book.Score.Points);
        Assert.Equal(expected.Games, book.Score.Games);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Match_navigation_keeps_each_history(int extraPreviousSwipes)
    {
        var book = new MatchBook();
        book.Award(Side.Left);
        book.NewMatch();
        book.Award(Side.Right);
        book.PreviousMatch();
        for (var i = 0; i < extraPreviousSwipes; i++) book.PreviousMatch();
        Assert.Equal(0, book.SelectedMatch);
        Assert.Equal("15", book.Score.PointText(Side.Left));
        book.Undo();
        book.NewMatch();
        Assert.Equal(3, book.Count);
        Assert.False(book.CanUndo);
        book.PreviousMatch();
        Assert.Equal("15", book.Score.PointText(Side.Right));
        book.PreviousMatch();
        Assert.Equal("0", book.Score.PointText(Side.Left));
    }

    [Theory]
    [InlineData(48, 0, 1, SwipeAction.NewMatch)]
    [InlineData(-48, 0, 1, SwipeAction.PreviousMatch)]
    [InlineData(47, 0, 1, SwipeAction.None)]
    [InlineData(96, 0, 2, SwipeAction.NewMatch)]
    [InlineData(-96, 0, 2, SwipeAction.PreviousMatch)]
    [InlineData(95, 0, 2, SwipeAction.None)]
    [InlineData(100, 80, 1, SwipeAction.None)]
    [InlineData(5, 100, 1, SwipeAction.None)]
    public void Only_deliberate_horizontal_swipes_navigate(float dx, float dy, float density, SwipeAction expected) =>
        Assert.Equal(expected, MatchSwipe.Recognize(dx, dy, density));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 99)]
    public void Invalid_saved_history_is_rejected(int selected, int side) =>
        Assert.Throws<ArgumentException>(() => new MatchBook(new MatchBookSnapshot
        {
            SelectedMatch = selected, Matches = [[(Side)side]]
        }));
}
