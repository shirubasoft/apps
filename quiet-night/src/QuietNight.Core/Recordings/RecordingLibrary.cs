using System.Globalization;
using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;

namespace QuietNight.Core.Recordings;

public sealed record Recording(string Path, NoiseClass Label, DateTimeOffset Created, TimeSpan Duration);

/// <summary>
/// The user's labelled recordings: 16 kHz WAV files whose names carry the label and time, e.g.
/// <c>cat-20261105T231502123.wav</c>.
/// </summary>
public sealed class RecordingLibrary(string directory)
{
    private const string TimeFormat = "yyyyMMdd'T'HHmmssfff";

    public string Directory { get; } = directory;

    public IReadOnlyList<Recording> List()
    {
        if (!System.IO.Directory.Exists(Directory)) return [];
        return
        [
            .. System.IO.Directory.EnumerateFiles(Directory, "*.wav")
                .Select(Parse)
                .OfType<Recording>()
                .OrderByDescending(r => r.Created),
        ];
    }

    public Recording Save(ReadOnlySpan<float> samples, NoiseClass label, DateTimeOffset created)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var name = $"{Slug(label)}-{created.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture)}.wav";
        var path = Path.Combine(Directory, name);
        var temporary = path + ".part";
        using (var stream = File.Create(temporary))
            WaveFile.Write(stream, samples, ListeningFormat.SampleRate);
        File.Move(temporary, path, overwrite: true);
        return new Recording(path, label, created, Duration(samples.Length));
    }

    public float[] Load(Recording recording)
    {
        using var stream = File.OpenRead(recording.Path);
        var (samples, sampleRate) = WaveFile.Read(stream);
        if (sampleRate != ListeningFormat.SampleRate)
            throw new InvalidDataException($"{Path.GetFileName(recording.Path)} is not a {ListeningFormat.SampleRate} Hz recording.");
        return samples;
    }

    public void Delete(Recording recording) => File.Delete(recording.Path);

    public static string Slug(NoiseClass label) => label switch
    {
        NoiseClass.Cat => "cat",
        NoiseClass.Motorcycle => "motorcycle",
        NoiseClass.Car => "car",
        _ => "other",
    };

    private static Recording? Parse(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var dash = name.LastIndexOf('-');
        if (dash <= 0) return null;
        NoiseClass? label = name[..dash] switch
        {
            "cat" => NoiseClass.Cat,
            "motorcycle" => NoiseClass.Motorcycle,
            "car" => NoiseClass.Car,
            "other" => NoiseClass.Other,
            _ => null,
        };
        if (label is not { } known ||
            !DateTime.TryParseExact(name[(dash + 1)..], TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var created))
            return null;
        var samples = Math.Max(0, (new FileInfo(path).Length - 44) / 2);
        return new Recording(path, known, new DateTimeOffset(created, TimeSpan.Zero), Duration(samples));
    }

    private static TimeSpan Duration(long samples) => TimeSpan.FromSeconds((double)samples / ListeningFormat.SampleRate);
}
