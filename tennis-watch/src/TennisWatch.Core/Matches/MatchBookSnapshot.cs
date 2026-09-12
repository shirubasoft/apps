using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using TennisWatch.Core.Scoring;

namespace TennisWatch.Core.Matches;

public sealed record MatchBookSnapshot
{
    public required int SelectedMatch { get; init; }
    public required Side[][] Matches { get; init; }
}

[JsonSerializable(typeof(MatchBookSnapshot))]
[ExcludeFromCodeCoverage]
internal partial class MatchJsonContext : JsonSerializerContext;
