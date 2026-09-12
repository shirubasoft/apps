using TennisWatch.Core.Scoring;

namespace TennisWatch.Core.Matches;

public sealed class MatchBook
{
    private readonly List<List<Side>> matches;

    public MatchBook() : this(new MatchBookSnapshot { SelectedMatch = 0, Matches = [[]] }) { }

    public MatchBook(MatchBookSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Matches is not { Length: > 0 } ||
            snapshot.SelectedMatch < 0 || snapshot.SelectedMatch >= snapshot.Matches.Length ||
            snapshot.Matches.Any(match => match is null || match.Any(side => !Enum.IsDefined(side))))
            throw new ArgumentException("Invalid match history.", nameof(snapshot));

        matches = snapshot.Matches.Select(match => match.ToList()).ToList();
        SelectedMatch = snapshot.SelectedMatch;
        Score = MatchScore.Replay(matches[SelectedMatch]);
    }

    public int SelectedMatch { get; private set; }
    public int Count => matches.Count;
    public MatchScore Score { get; private set; }
    public bool CanUndo => matches[SelectedMatch].Count > 0;

    public void Award(Side side)
    {
        Score = Score.Award(side);
        matches[SelectedMatch].Add(side);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        matches[SelectedMatch].RemoveAt(matches[SelectedMatch].Count - 1);
        Score = MatchScore.Replay(matches[SelectedMatch]);
    }

    public void NewMatch()
    {
        matches.Add([]);
        SelectedMatch = matches.Count - 1;
        Score = new MatchScore();
    }

    public void PreviousMatch()
    {
        SelectedMatch = Math.Max(0, SelectedMatch - 1);
        Score = MatchScore.Replay(matches[SelectedMatch]);
    }

    public MatchBookSnapshot Snapshot() => new()
    {
        SelectedMatch = SelectedMatch,
        Matches = matches.Select(match => match.ToArray()).ToArray()
    };
}
