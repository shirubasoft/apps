using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using TennisWatch.Core.Scoring;

namespace TennisWatch.Core.Matches;

public sealed record MatchBookSnapshot
{
    public int Version { get; init; } = 2;
    public required int SelectedMatch { get; init; }
    public required int NextMatchNumber { get; init; }
    public required MatchSnapshot[] Matches { get; init; }
}

public sealed record MatchSnapshot
{
    public required int Number { get; init; }
    public required DateTimeOffset? StartedAt { get; init; }
    public required Side[] Points { get; init; }
}

internal sealed record LegacyMatchBookSnapshot
{
    public required int SelectedMatch { get; init; }
    public required Side[][] Matches { get; init; }
}

[JsonSerializable(typeof(MatchBookSnapshot))]
[JsonSerializable(typeof(LegacyMatchBookSnapshot))]
[ExcludeFromCodeCoverage]
internal partial class MatchJsonContext : JsonSerializerContext;
