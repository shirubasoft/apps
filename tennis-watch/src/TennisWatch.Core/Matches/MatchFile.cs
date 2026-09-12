using System.Text.Json;

namespace TennisWatch.Core.Matches;

public sealed class MatchFile(string path)
{
    public MatchBook Load()
    {
        if (!File.Exists(path)) return new MatchBook();
        var snapshot = JsonSerializer.Deserialize(File.ReadAllText(path), MatchJsonContext.Default.MatchBookSnapshot)
            ?? throw new JsonException("Match history is empty.");
        return new MatchBook(snapshot);
    }

    public void Save(MatchBook book)
    {
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(book.Snapshot(), MatchJsonContext.Default.MatchBookSnapshot));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
