using TennisWatch.Core.Matches;
using TennisWatch.Core.Scoring;

namespace TennisWatch.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("tennis-watch-tests-").FullName;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Restart_restores_selected_match_and_undo_history(bool viewPrevious)
    {
        var file = new MatchFile(Path.Combine(directory, "matches.json"));
        var book = file.Load(MatchSamples.StartedAt);
        foreach (var side in ScoringTests.Games("LRLRLRLRLRLR").Concat(ScoringTests.Sides("LLLLLLL"))) book.Award(side);
        book.NextMatch(MatchSamples.StartedAt.AddDays(1));
        book.Award(Side.Right);
        if (viewPrevious) book.PreviousMatch();
        file.Save(book);
        var restored = file.Load(MatchSamples.StartedAt.AddDays(5));
        Assert.Equal(book.SelectedMatch, restored.SelectedMatch);
        Assert.Equal(2, restored.Count);
        Assert.Equal(book.Current.StartedAt, restored.Current.StartedAt);
        Assert.Equal(book.Current.Number, restored.Current.Number);
        restored.Undo();
        if (viewPrevious)
        {
            Assert.True(restored.Score.IsTiebreak);
            Assert.Equal("6", restored.Score.PointText(Side.Left));
            Assert.Empty(restored.Score.CompletedSets);
        }
        else
            Assert.False(restored.CanUndo);
        file.Save(restored);
        Assert.Equal(restored.Score.Points, file.Load(MatchSamples.StartedAt).Score.Points);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    public void Unfinished_write_does_not_replace_saved_match(string temporaryContent)
    {
        var path = Path.Combine(directory, "matches.json");
        var file = new MatchFile(path);
        var book = file.Load(MatchSamples.StartedAt);
        book.Award(Side.Left);
        file.Save(book);
        File.WriteAllText(path + ".tmp", temporaryContent);
        Assert.Equal("15", file.Load(MatchSamples.StartedAt).Score.PointText(Side.Left));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Legacy_matches_keep_scores_and_receive_numbers_without_invented_dates(int selected)
    {
        var path = Path.Combine(directory, "matches.json");
        File.WriteAllText(path, $$"""{"SelectedMatch":{{selected}},"Matches":[[0,1],[1,1]]}""");
        var file = new MatchFile(path);
        var book = file.Load(MatchSamples.StartedAt);
        Assert.Equal(selected, book.SelectedMatch);
        Assert.Equal(selected + 1, book.Current.Number);
        Assert.Null(book.Current.StartedAt);
        Assert.Equal(selected == 0 ? "15" : "30", book.Score.PointText(Side.Right));
        file.Save(book);
        var restored = file.Load(MatchSamples.StartedAt.AddDays(5));
        Assert.Null(restored.Current.StartedAt);
        Assert.Equal(2, restored.Snapshot().Version);
        restored.NextMatch(MatchSamples.StartedAt);
        if (selected == 0) restored.NextMatch(MatchSamples.StartedAt);
        Assert.Equal(3, restored.Current.Number);
        Assert.Equal(MatchSamples.StartedAt, restored.Current.StartedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deletion_and_next_number_survive_restart(bool deleteAll)
    {
        var file = new MatchFile(Path.Combine(directory, "matches.json"));
        var book = MatchSamples.ThreeMatches();
        book.DeleteCurrent(MatchSamples.StartedAt);
        if (deleteAll)
        {
            book.DeleteCurrent(MatchSamples.StartedAt);
            book.DeleteCurrent(MatchSamples.StartedAt);
        }
        file.Save(book);
        var restored = file.Load(MatchSamples.StartedAt.AddDays(1));
        Assert.Equal(book.Current.Number, restored.Current.Number);
        Assert.Equal(book.Count, restored.Count);
        restored.NextMatch(MatchSamples.StartedAt.AddDays(1));
        Assert.Equal(deleteAll ? 5 : 4, restored.Current.Number);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
