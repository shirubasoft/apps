using System.Globalization;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;
using QuietNight.Core.Recordings;
using QuietNight.Core.Training;

namespace QuietNight.Training;

/// <summary>
/// Where the models live: the prebuilt YAMNet model and reference heads ship inside the app; heads trained on the
/// user's recordings are saved in app storage and used instead until the user resets them.
/// </summary>
public static class ModelStore
{
    private const string PersonalInfoFile = "personal.txt";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static byte[]? yamnet;
    private static float[][]? layers;

    public static string PersonalDirectory => Path.Combine(FileSystem.AppDataDirectory, "model");

    public static RecordingLibrary Recordings { get; } = new(Path.Combine(FileSystem.AppDataDirectory, "recordings"));

    public static bool IsPersonal => File.Exists(Path.Combine(PersonalDirectory, DetectorHeads.SoundFile));

    /// <summary>When the personal heads were trained and from how many recordings, or null for the prebuilt heads.</summary>
    public static (DateTimeOffset Trained, int Recordings)? PersonalInfo
    {
        get
        {
            var path = Path.Combine(PersonalDirectory, PersonalInfoFile);
            if (!IsPersonal || !File.Exists(path)) return null;
            var parts = File.ReadAllText(path).Split(' ');
            return parts.Length == 2 && DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) && int.TryParse(parts[1], out var count)
                ? (at, count)
                : null;
        }
    }

    public static async Task<byte[]> YamnetAsync()
    {
        await Gate.WaitAsync();
        try
        {
            return yamnet ??= await ReadAssetAsync("yamnet.onnx");
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>The masking noise layers, built once per process.</summary>
    public static float[][] Layers() => layers ??= NoiseLoop.CreateLayers(rmsDb: Night.NightSettings.LoopLevelDb);

    public static async Task<DetectorHeads> HeadsAsync()
    {
        if (IsPersonal)
        {
            try
            {
                return DetectorHeads.Load(name => File.OpenRead(Path.Combine(PersonalDirectory, name)));
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
            {
                Reset();
            }
        }
        return await BundledHeadsAsync();
    }

    public static async Task<DetectorHeads> BundledHeadsAsync()
    {
        var frame = new MemoryStream(await ReadAssetAsync(DetectorHeads.FrameFile));
        var sound = new MemoryStream(await ReadAssetAsync(DetectorHeads.SoundFile));
        return DetectorHeads.Load(name => name == DetectorHeads.FrameFile ? frame : sound);
    }

    public static async Task<ReferenceExamples> ReferenceAsync()
    {
        var files = new Dictionary<string, byte[]>();
        foreach (var name in new[] { DetectorHeads.FrameReplayFile, DetectorHeads.SoundReplayFile, DetectorHeads.FrameHeldOutFile, DetectorHeads.SoundHeldOutFile })
            files[name] = await ReadAssetAsync(name);
        return DetectorHeads.LoadReference(name => new MemoryStream(files[name]));
    }

    public static void SavePersonal(DetectorHeads heads, int recordings)
    {
        heads.Save(PersonalDirectory);
        File.WriteAllText(Path.Combine(PersonalDirectory, PersonalInfoFile),
            $"{DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture)} {recordings}");
    }

    public static void Reset()
    {
        if (Directory.Exists(PersonalDirectory))
            Directory.Delete(PersonalDirectory, recursive: true);
    }

    private static async Task<byte[]> ReadAssetAsync(string name)
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(name);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }
}
