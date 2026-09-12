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
        var book = file.Load();
        foreach (var side in ScoringTests.Games("LRLRLRLRLRLR").Concat(ScoringTests.Sides("LLLLLLL"))) book.Award(side);
        book.NewMatch();
        book.Award(Side.Right);
        if (viewPrevious) book.PreviousMatch();
        file.Save(book);
        var restored = file.Load();
        Assert.Equal(book.SelectedMatch, restored.SelectedMatch);
        Assert.Equal(2, restored.Count);
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
        Assert.Equal(restored.Score.Points, file.Load().Score.Points);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    public void Unfinished_write_does_not_replace_saved_match(string temporaryContent)
    {
        var path = Path.Combine(directory, "matches.json");
        var file = new MatchFile(path);
        var book = file.Load();
        book.Award(Side.Left);
        file.Save(book);
        File.WriteAllText(path + ".tmp", temporaryContent);
        Assert.Equal("15", file.Load().Score.PointText(Side.Left));
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
