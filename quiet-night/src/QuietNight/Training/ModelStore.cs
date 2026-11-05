using System.Globalization;
using System.Security.Cryptography;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;
using QuietNight.Core.Recordings;
using QuietNight.Core.Training;

namespace QuietNight.Training;

/// <summary>
/// Where the models live: the sound model and reference heads ship inside the app; heads trained on the user's
/// recordings are saved in app storage and used instead until the user resets them. Personal heads remember which
/// sound model they were trained on: after an update that changes it, they no longer fit its features, so they are
/// set aside and the user's recordings stay ready to train again. Personal heads and their record are published
/// together by renaming a finished folder, and every read and change of them takes the same lock.
/// </summary>
public static class ModelStore
{
    private const string PersonalInfoFile = "personal.txt";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly SemaphoreSlim PersonalGate = new(1, 1);
    private static byte[]? yamnet;
    private static string? soundModelId;
    private static float[][]? layers;

    public static string PersonalDirectory => Path.Combine(FileSystem.AppDataDirectory, "model");

    public static RecordingLibrary Recordings { get; } = new(Path.Combine(FileSystem.AppDataDirectory, "recordings"));

    public static bool IsPersonal => File.Exists(Path.Combine(PersonalDirectory, DetectorHeads.SoundFile));

    /// <summary>When the personal heads were trained and from how many recordings, or null for the prebuilt heads.</summary>
    public static (DateTimeOffset Trained, int Recordings)? PersonalInfo => ReadPersonalInfo() is { } info ? (info.Trained, info.Recordings) : null;

    public static async Task<byte[]> YamnetAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (yamnet is null)
            {
                yamnet = await ReadAssetAsync("sound-model.onnx");
                soundModelId = Convert.ToHexString(SHA256.HashData(yamnet))[..16];
            }
            return yamnet;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Sets aside personal heads trained on a different sound model, or saved before heads recorded theirs.</summary>
    public static async Task ForgetIncompatibleAsync()
    {
        await YamnetAsync();
        await PersonalGate.WaitAsync();
        try
        {
            ForgetIncompatible();
        }
        finally
        {
            PersonalGate.Release();
        }
    }

    /// <summary>The masking noise layers, built once per process.</summary>
    public static float[][] Layers() => layers ??= NoiseLoop.CreateLayers(rmsDb: Night.NightSettings.LoopLevelDb);

    public static async Task<DetectorHeads> HeadsAsync()
    {
        await YamnetAsync();
        await PersonalGate.WaitAsync();
        try
        {
            ForgetIncompatible();
            if (IsPersonal)
                return DetectorHeads.Load(name => File.OpenRead(Path.Combine(PersonalDirectory, name)));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            DeletePersonal();
        }
        finally
        {
            PersonalGate.Release();
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

    /// <summary>Writes the heads and their record to a fresh folder, then swaps it in for the current personal heads.</summary>
    public static async Task SavePersonalAsync(DetectorHeads heads, int recordings)
    {
        await YamnetAsync();
        await PersonalGate.WaitAsync();
        try
        {
            var staging = PersonalDirectory + ".new";
            var previous = PersonalDirectory + ".old";
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            heads.Save(staging);
            File.WriteAllText(Path.Combine(staging, PersonalInfoFile),
                $"{DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture)} {recordings} {soundModelId}");
            if (Directory.Exists(previous)) Directory.Delete(previous, recursive: true);
            if (Directory.Exists(PersonalDirectory)) Directory.Move(PersonalDirectory, previous);
            Directory.Move(staging, PersonalDirectory);
            if (Directory.Exists(previous)) Directory.Delete(previous, recursive: true);
        }
        finally
        {
            PersonalGate.Release();
        }
    }

    private static (DateTimeOffset Trained, int Recordings, string SoundModel)? ReadPersonalInfo()
    {
        var path = Path.Combine(PersonalDirectory, PersonalInfoFile);
        if (!IsPersonal || !File.Exists(path)) return null;
        var parts = File.ReadAllText(path).Split(' ');
        return parts.Length == 3 && DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) && int.TryParse(parts[1], out var count)
            ? (at, count, parts[2])
            : null;
    }

    public static async Task ResetAsync()
    {
        await PersonalGate.WaitAsync();
        try
        {
            DeletePersonal();
        }
        finally
        {
            PersonalGate.Release();
        }
    }

    private static void ForgetIncompatible()
    {
        if (IsPersonal && ReadPersonalInfo()?.SoundModel != soundModelId) DeletePersonal();
    }

    private static void DeletePersonal()
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
