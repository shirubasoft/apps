using TennisWatch.Core.Scoring;

namespace TennisWatch.Core.Matches;

public sealed record MatchSummary
{
    public required int Number { get; init; }
    public required DateTimeOffset? StartedAt { get; init; }
    public required MatchScore Score { get; init; }
    public required bool CanUndo { get; init; }
}
