using System.Text.Json;

namespace PenSketch.Core.Documents;

public sealed class DocumentStore(string path)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public async Task SaveAsync(SketchDocument document)
    {
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(document, Options);
            await File.WriteAllTextAsync(path + ".tmp", json);
            File.Move(path + ".tmp", path, true);
        }
        finally { gate.Release(); }
    }
    public async Task<SketchDocument> LoadAsync()
    {
        if (!File.Exists(path)) return new();
        var document = JsonSerializer.Deserialize<SketchDocument>(await File.ReadAllTextAsync(path), Options)
            ?? throw new InvalidDataException("The saved sketch is empty.");
        if (document.Version != 1 || document.Width != 360 || document.Height != 640 || document.Elements is null || document.EndPoses is null)
            throw new InvalidDataException("This sketch uses an unsupported format.");
        return document;
    }
}
