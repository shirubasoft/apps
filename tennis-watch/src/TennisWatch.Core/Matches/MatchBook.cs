using TennisWatch.Core.Scoring;

namespace TennisWatch.Core.Matches;

public sealed class MatchBook
{
    private sealed record Match(int Number, DateTimeOffset? StartedAt, List<Side> Points);
    private readonly List<Match> matches;

    public MatchBook(DateTimeOffset now) : this(new MatchBookSnapshot
    {
        SelectedMatch = 0, NextMatchNumber = 2,
        Matches = [new() { Number = 1, StartedAt = now, Points = [] }]
    }) { }

    public MatchBook(MatchBookSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSelection(snapshot);
        foreach (var match in snapshot.Matches) ValidateMatch(match);
        if (snapshot.Matches.Select(match => match.Number).Distinct().Count() != snapshot.Matches.Length ||
            snapshot.NextMatchNumber <= snapshot.Matches.Max(match => match.Number))
            throw new ArgumentException("Invalid match numbering.", nameof(snapshot));

        matches = snapshot.Matches.Select(match => new Match(match.Number, match.StartedAt, match.Points.ToList())).ToList();
        SelectedMatch = snapshot.SelectedMatch;
        NextMatchNumber = snapshot.NextMatchNumber;
        Score = MatchScore.Replay(matches[SelectedMatch].Points);
    }

    public int SelectedMatch { get; private set; }
    public int Count => matches.Count;
    public int NextMatchNumber { get; private set; }
    public MatchScore Score { get; private set; }
    public bool CanUndo => matches[SelectedMatch].Points.Count > 0;
    public bool HasPrevious => SelectedMatch > 0;
    public MatchSummary Current => Summary(SelectedMatch);

    public MatchSummary PreviewNext(DateTimeOffset now) => SelectedMatch + 1 < Count
        ? Summary(SelectedMatch + 1)
        : new() { Number = NextMatchNumber, StartedAt = now, Score = new MatchScore(), CanUndo = false };

    public MatchSummary PreviewPrevious() => Summary(Math.Max(0, SelectedMatch - 1));

    private static void ValidateSelection(MatchBookSnapshot snapshot)
    {
        if (snapshot.Version != 2 || snapshot.Matches is not { Length: > 0 } ||
            snapshot.SelectedMatch < 0 || snapshot.SelectedMatch >= snapshot.Matches.Length)
            throw new ArgumentException("Invalid match selection.", nameof(snapshot));
    }

    private static void ValidateMatch(MatchSnapshot match)
    {
        if (match is null || match.Number < 1 || match.Points is null ||
            match.Points.Any(side => !Enum.IsDefined(side)))
            throw new ArgumentException("Invalid match history.", nameof(match));
    }

    private MatchSummary Summary(int index) => new()
    {
        Number = matches[index].Number, StartedAt = matches[index].StartedAt,
        Score = index == SelectedMatch ? Score : MatchScore.Replay(matches[index].Points),
        CanUndo = matches[index].Points.Count > 0
    };

    public void Award(Side side)
    {
        Score = Score.Award(side);
        matches[SelectedMatch].Points.Add(side);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        matches[SelectedMatch].Points.RemoveAt(matches[SelectedMatch].Points.Count - 1);
        Score = MatchScore.Replay(matches[SelectedMatch].Points);
    }

    public void NextMatch(DateTimeOffset now)
    {
        if (SelectedMatch == matches.Count - 1) CreateMatch(now);
        else Select(SelectedMatch + 1);
    }

    public void PreviousMatch()
    {
        Select(Math.Max(0, SelectedMatch - 1));
    }

    public void DeleteCurrent(DateTimeOffset now)
    {
        matches.RemoveAt(SelectedMatch);
        if (matches.Count == 0) CreateMatch(now);
        else Select(Math.Max(0, SelectedMatch - 1));
    }

    private void CreateMatch(DateTimeOffset now)
    {
        var number = NextMatchNumber;
        NextMatchNumber = checked(number + 1);
        matches.Add(new Match(number, now, []));
        Select(matches.Count - 1);
    }

    private void Select(int index)
    {
        SelectedMatch = index;
        Score = MatchScore.Replay(matches[index].Points);
    }

    public MatchBookSnapshot Snapshot() => new()
    {
        SelectedMatch = SelectedMatch,
        NextMatchNumber = NextMatchNumber,
        Matches = matches.Select(match => new MatchSnapshot
        {
            Number = match.Number, StartedAt = match.StartedAt, Points = match.Points.ToArray()
        }).ToArray()
    };
}
