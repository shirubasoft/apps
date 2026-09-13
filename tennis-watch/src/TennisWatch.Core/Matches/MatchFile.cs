using System.Text.Json;

namespace TennisWatch.Core.Matches;

public sealed class MatchFile(string path)
{
    public MatchBook Load(DateTimeOffset now)
    {
        if (!File.Exists(path)) return new MatchBook(now);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("Version", out _))
            return LoadLegacy(document.RootElement);
        var snapshot = document.RootElement.Deserialize(MatchJsonContext.Default.MatchBookSnapshot)
            ?? throw new JsonException("Match history is empty.");
        return new MatchBook(snapshot);
    }

    private static MatchBook LoadLegacy(JsonElement json)
    {
        var legacy = json.Deserialize(MatchJsonContext.Default.LegacyMatchBookSnapshot)
            ?? throw new JsonException("Match history is empty.");
        if (legacy.Matches is null || legacy.Matches.Any(points => points is null))
            throw new JsonException("Match history is invalid.");
        return new MatchBook(new MatchBookSnapshot
        {
            SelectedMatch = legacy.SelectedMatch, NextMatchNumber = legacy.Matches.Length + 1,
            Matches = legacy.Matches.Select((points, index) => new MatchSnapshot
            {
                Number = index + 1, StartedAt = null, Points = points
            }).ToArray()
        });
    }

    public void Save(MatchBook book)
    {
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(book.Snapshot(), MatchJsonContext.Default.MatchBookSnapshot));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
