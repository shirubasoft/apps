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
        var book = new MatchBook(MatchSamples.StartedAt);
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
        var book = new MatchBook(MatchSamples.StartedAt);
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
        var book = new MatchBook(MatchSamples.StartedAt);
        book.Award(Side.Left);
        book.NextMatch(MatchSamples.StartedAt.AddDays(1));
        book.Award(Side.Right);
        book.PreviousMatch();
        for (var i = 0; i < extraPreviousSwipes; i++) book.PreviousMatch();
        Assert.Equal(0, book.SelectedMatch);
        Assert.Equal("15", book.Score.PointText(Side.Left));
        book.Undo();
        book.NextMatch(MatchSamples.StartedAt.AddDays(2));
        Assert.Equal(2, book.Count);
        Assert.Equal("15", book.Score.PointText(Side.Right));
        Assert.Equal(MatchSamples.StartedAt.AddDays(1), book.Current.StartedAt);
        book.NextMatch(MatchSamples.StartedAt.AddDays(2));
        Assert.Equal(3, book.Count);
        Assert.False(book.CanUndo);
        book.PreviousMatch();
        book.PreviousMatch();
        Assert.Equal("0", book.Score.PointText(Side.Left));
    }

    [Theory]
    [InlineData(48, 0, SwipeAction.PreviousMatch)]
    [InlineData(-48, 0, SwipeAction.NextMatch)]
    [InlineData(47, 0, SwipeAction.None)]
    [InlineData(-47, 0, SwipeAction.None)]
    [InlineData(100, 80, SwipeAction.None)]
    [InlineData(5, 100, SwipeAction.None)]
    public void Only_deliberate_horizontal_swipes_navigate(float dx, float dy, SwipeAction expected) =>
        Assert.Equal(expected, MatchSwipe.Recognize(SwipeAxis.Horizontal, dx, dy));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 99)]
    public void Invalid_saved_history_is_rejected(int selected, int side) =>
        Assert.Throws<ArgumentException>(() => new MatchBook(new MatchBookSnapshot
        {
            SelectedMatch = selected, NextMatchNumber = 2,
            Matches = [new() { Number = 1, StartedAt = MatchSamples.StartedAt, Points = [(Side)side] }]
        }));

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void Delete_keeps_neighbor_histories_and_stable_numbers(int selected, int expectedNumber)
    {
        var book = MatchSamples.ThreeMatches();
        while (book.SelectedMatch > selected) book.PreviousMatch();
        book.DeleteCurrent(MatchSamples.StartedAt.AddDays(3));
        Assert.Equal(2, book.Count);
        Assert.Equal(expectedNumber, book.Current.Number);
        Assert.DoesNotContain(book.Snapshot().Matches, match => match.Number == selected + 1);
        Assert.Equal(MatchSamples.StartedAt.AddDays(expectedNumber - 1), book.Current.StartedAt);
        Assert.Equal(expectedNumber == 1 ? "15" : "0", book.Score.PointText(Side.Left));
        while (book.SelectedMatch < book.Count - 1) book.NextMatch(MatchSamples.StartedAt);
        book.NextMatch(MatchSamples.StartedAt.AddDays(4));
        Assert.Equal(4, book.Current.Number);
        Assert.Equal(3, book.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Deleting_the_only_match_leaves_a_fresh_match(int deletions)
    {
        var book = new MatchBook(MatchSamples.StartedAt);
        book.Award(Side.Left);
        for (var i = 1; i <= deletions; i++) book.DeleteCurrent(MatchSamples.StartedAt.AddDays(i));
        Assert.Equal(1, book.Count);
        Assert.Equal(0, book.SelectedMatch);
        Assert.Equal(deletions + 1, book.Current.Number);
        Assert.Equal(MatchSamples.StartedAt.AddDays(deletions), book.Current.StartedAt);
        Assert.False(book.CanUndo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Previews_do_not_change_selection_dates_or_point_history(int selected)
    {
        var book = MatchSamples.ThreeMatches();
        while (book.SelectedMatch > selected) book.PreviousMatch();
        var next = book.PreviewNext(MatchSamples.StartedAt.AddDays(3));
        var previous = book.PreviewPrevious();
        Assert.Equal(selected + 2, next.Number);
        Assert.Equal(Math.Max(1, selected), previous.Number);
        Assert.Equal(selected, book.SelectedMatch);
        Assert.Equal(3, book.Count);
        Assert.Equal(MatchSamples.StartedAt.AddDays(selected + 1), next.StartedAt);
        Assert.Equal(selected < 2, next.CanUndo);
        Assert.Equal(selected > 0, book.HasPrevious);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Snapshots_do_not_share_mutable_point_arrays(bool alterInput)
    {
        var snapshot = MatchSamples.ThreeMatches().Snapshot();
        var book = new MatchBook(snapshot);
        var points = alterInput ? snapshot.Matches[2].Points : book.Snapshot().Matches[2].Points;
        points[0] = Side.Right;
        book.Undo();
        Assert.Equal("15", book.Score.PointText(Side.Left));
    }

    public static IEnumerable<object[]> InvalidSnapshots()
    {
        var valid = MatchSamples.ThreeMatches().Snapshot();
        yield return [valid with { Version = 3 }];
        yield return [valid with { Matches = [] }];
        yield return [valid with { Matches = null! }];
        yield return [valid with { NextMatchNumber = 3 }];
        yield return [valid with { Matches = [valid.Matches[0], valid.Matches[0], valid.Matches[2]] }];
        yield return [valid with { Matches = [null!, valid.Matches[1], valid.Matches[2]] }];
        yield return [valid with { Matches = [valid.Matches[0] with { Number = 0 }, valid.Matches[1], valid.Matches[2]] }];
        yield return [valid with { Matches = [valid.Matches[0] with { Points = null! }, valid.Matches[1], valid.Matches[2]] }];
    }

    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void Invalid_metadata_is_rejected(MatchBookSnapshot snapshot) =>
        Assert.Throws<ArgumentException>(() => new MatchBook(snapshot));
}
