using QuietNight.Core.Training;

namespace QuietNight.Tests.Support;

/// <summary>Decoded reference sounds of one split, stored as Opus under <c>group/id.opus</c>.</summary>
public sealed class ReferenceClips(string directory, string split)
{
    private readonly Dictionary<int, float[]> decoded = [];

    /// <summary>The committed held-out test sounds, used only by the final evaluation.</summary>
    public static ReferenceClips Test { get; } = new(Path.Combine(AppContext.BaseDirectory, "ReferenceSounds"), "test");

    public string Split { get; } = split;

    public IReadOnlyList<ReferenceSound> Group(string group) =>
        [.. TestAssets.Manifest.Where(s => s.Split == Split && s.Group == group && File.Exists(PathOf(s))).OrderBy(s => s.Id)];

    public float[] Audio(ReferenceSound sound)
    {
        lock (decoded)
        {
            if (!decoded.TryGetValue(sound.Id, out var samples))
                decoded[sound.Id] = samples = OpusAudio.Decode(PathOf(sound));
            return samples;
        }
    }

    private string PathOf(ReferenceSound sound) => Path.Combine(directory, sound.Group, $"{sound.Id}.opus");
}
