using QuietNight.Core.Detection;
using QuietNight.Core.Training;

namespace QuietNight.Tests.Support;

/// <summary>The bundled models and the reference-sound manifest copied next to the tests.</summary>
public static class TestAssets
{
    private static readonly Lazy<byte[]> YamnetBytes = new(() => File.ReadAllBytes(Model("yamnet.onnx")));
    private static readonly Lazy<IReadOnlyList<ReferenceSound>> Sounds = new(() =>
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "ReferenceSounds", "manifest.json"));
        return ReferenceSound.ReadManifest(stream);
    });

    public static IReadOnlyList<ReferenceSound> Manifest => Sounds.Value;

    public static SoundModel CreateSoundModel() => new(YamnetBytes.Value, threads: 1);

    public static DetectorHeads Heads() => DetectorHeads.Load(name => File.OpenRead(Model(name)));

    public static ReferenceExamples Reference() => DetectorHeads.LoadReference(name => File.OpenRead(Model(name)));

    private static string Model(string name) => Path.Combine(AppContext.BaseDirectory, "Models", name);
}
