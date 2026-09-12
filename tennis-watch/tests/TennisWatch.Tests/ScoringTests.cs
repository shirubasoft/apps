using TennisWatch.Core.Scoring;

namespace TennisWatch.Tests;

public sealed class ScoringTests
{
    [Theory]
    [InlineData("", "0", "0", 0, 0)]
    [InlineData("L", "15", "0", 0, 0)]
    [InlineData("LRR", "15", "30", 0, 0)]
    [InlineData("LLL", "40", "0", 0, 0)]
    [InlineData("LLLL", "0", "0", 1, 0)]
    [InlineData("RRRR", "0", "0", 0, 1)]
    [InlineData("LLLRRR", "40", "40", 0, 0)]
    [InlineData("LLLRRRL", "AD", "40", 0, 0)]
    [InlineData("LLLRRRR", "40", "AD", 0, 0)]
    [InlineData("LLLRRRLR", "40", "40", 0, 0)]
    [InlineData("LLLRRRRL", "40", "40", 0, 0)]
    [InlineData("LLLRRRLL", "0", "0", 1, 0)]
    [InlineData("LLLRRRRR", "0", "0", 0, 1)]
    [InlineData("LLLRRRLRLRLRRR", "0", "0", 0, 1)]
    public void Points_follow_advantage_scoring(string points, string left, string right, int leftGames, int rightGames)
    {
        var score = MatchScore.Replay(Sides(points));
        Assert.Equal(left, score.PointText(Side.Left));
        Assert.Equal(right, score.PointText(Side.Right));
        Assert.Equal(new ScorePair { Left = leftGames, Right = rightGames }, score.Games);
        Assert.Empty(score.CompletedSets);
    }

    [Theory]
    [InlineData("LLLLLL", 6, 0)]
    [InlineData("RRRRRR", 0, 6)]
    [InlineData("LLRRLLRRLL", 6, 4)]
    [InlineData("LLLLLRRRRRLL", 7, 5)]
    [InlineData("RRRRRLLLLLRR", 5, 7)]
    public void Set_requires_six_games_and_two_game_lead(string games, int left, int right)
    {
        var score = MatchScore.Replay(Games(games));
        Assert.Equal(new ScorePair { Left = left, Right = right }, Assert.Single(score.CompletedSets));
        Assert.Equal(default, score.Games);
        Assert.Equal(default, score.Points);
    }

    [Theory]
    [InlineData("LLLLLRRRRRL", 6, 5, false)]
    [InlineData("LLLLLRRRRRR", 5, 6, false)]
    [InlineData("LRLRLRLRLRLR", 6, 6, true)]
    public void Close_sets_continue(string games, int left, int right, bool tiebreak)
    {
        var score = MatchScore.Replay(Games(games));
        Assert.Empty(score.CompletedSets);
        Assert.Equal(new ScorePair { Left = left, Right = right }, score.Games);
        Assert.Equal(tiebreak, score.IsTiebreak);
    }

    [Theory]
    [InlineData("LLLLLL", "6", "0", false, 0, 0)]
    [InlineData("LLLLLLL", "0", "0", true, 7, 6)]
    [InlineData("RRRRRRR", "0", "0", true, 6, 7)]
    [InlineData("LRLRLRLRLRLRL", "7", "6", false, 0, 0)]
    [InlineData("LRLRLRLRLRLRLL", "0", "0", true, 7, 6)]
    [InlineData("LRLRLRLRLRLRRR", "0", "0", true, 6, 7)]
    [InlineData("LRLRLRLRLRLRLRLRLRLRL", "11", "10", false, 0, 0)]
    public void Tiebreak_requires_seven_points_and_two_point_lead(string points, string left, string right,
        bool finishedSet, int leftGames, int rightGames)
    {
        var score = MatchScore.Replay(Games("LRLRLRLRLRLR").Concat(Sides(points)));
        Assert.Equal(left, score.PointText(Side.Left));
        Assert.Equal(right, score.PointText(Side.Right));
        Assert.Equal(!finishedSet, score.IsTiebreak);
        if (finishedSet)
            Assert.Equal(new ScorePair { Left = leftGames, Right = rightGames }, Assert.Single(score.CompletedSets));
        else
            Assert.Empty(score.CompletedSets);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public void Scoring_continues_after_any_number_of_sets(int count)
    {
        var score = MatchScore.Replay(Games(new string('L', count * 6)).Concat(Sides("R")));
        Assert.Equal(count, score.CompletedSets.Length);
        Assert.Equal("15", score.PointText(Side.Right));
    }

    internal static IEnumerable<Side> Sides(string points) => points.Select(point => point == 'L' ? Side.Left : Side.Right);
    internal static IEnumerable<Side> Games(string games) => Sides(games).SelectMany(side => Enumerable.Repeat(side, 4));
}
