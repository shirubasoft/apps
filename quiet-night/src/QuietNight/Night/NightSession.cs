using System.Globalization;
using Android.Content;
using QuietNight.Core.Detection;
using QuietNight.Core.Recordings;
using QuietNight.Training;

namespace QuietNight.Night;

/// <summary>Connects the screen to the night service, which may outlive any page.</summary>
public static class NightSession
{
    private const string LastNightKey = "last-night";
    private const string HeartbeatKey = "night-heartbeat";
    private static readonly TimeSpan BeatEvery = TimeSpan.FromMinutes(1);
    private static long lastBeat;
    private static int beatMasked = -1;
    private static NightStatus status = NightStatus.Idle;
    private static long startingSince;

    public static NightStatus Status => Volatile.Read(ref status);

    /// <summary>
    /// True from the moment the user starts a night until the service reports that it runs, stopped or failed. Stop
    /// always reaches the service, so a start that never comes can be cancelled.
    /// </summary>
    public static bool Starting => Interlocked.Read(ref startingSince) != 0;

    /// <summary>How long the night has been starting, or zero when it is not.</summary>
    public static TimeSpan StartingFor => Interlocked.Read(ref startingSince) is var since and not 0
        ? TimeSpan.FromMilliseconds(Environment.TickCount64 - since)
        : TimeSpan.Zero;

    /// <summary>The most recent finished night, kept across restarts so the morning screen can show it.</summary>
    public static NightSummary? LastNight => Read(LastNightKey);

    /// <summary>
    /// Finds a night that ended without being stopped, as when Android closes the app overnight: the night keeps a
    /// heartbeat while it runs, and one left behind means it never reached its end. The night is kept as the last
    /// night, ending at its last heartbeat, and the time it stopped is returned; null when there was none.
    /// </summary>
    public static DateTimeOffset? RecoverInterrupted()
    {
        if (Status.Running || Starting || Read(HeartbeatKey) is not { } night) return null;
        Preferences.Default.Remove(HeartbeatKey);
        Write(LastNightKey, night);
        return night.Ended;
    }

    private static NightSummary? Read(string key)
    {
        // The first line holds when the night ran; each further line one masked noise: time, kind and boost.
        var lines = Preferences.Default.Get(key, "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;
        var span = lines[0].Split(' ');
        if (span.Length is < 2 or > 3 || !TryParseTime(span[0], out var started) || !TryParseTime(span[1], out var ended)) return null;
        var maxBoostDb = span.Length == 3 && float.TryParse(span[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var max) ? max : 30;
        var masked = new List<MaskedNoise>();
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(' ');
            if (parts.Length == 3 && TryParseTime(parts[0], out var at) && Enum.TryParse<NoiseClass>(parts[1], out var noise)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var boost))
                masked.Add(new MaskedNoise(noise, at, boost));
        }
        return new NightSummary(started, ended, masked, maxBoostDb);
    }

    internal static NightEngine? Engine { get; set; }

    /// <summary>
    /// Clips saved from the lock screen when nothing had just been masked, so nobody has said what they are yet. They
    /// are written to the phone at once, so none is lost if the app closes; the page asks about each when opened.
    /// </summary>
    private static string UnsortedDirectory => Path.Combine(FileSystem.AppDataDirectory, "unsorted");

    /// <summary>Keeps a clip until the user says what it is; throws if the phone cannot write it.</summary>
    public static void KeepUnsorted(float[] samples)
    {
        Directory.CreateDirectory(UnsortedDirectory);
        var path = Path.Combine(UnsortedDirectory, $"{DateTimeOffset.Now.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff", CultureInfo.InvariantCulture)}.wav");
        using (var stream = File.Create(path + ".part"))
            WaveFile.Write(stream, samples, Core.Analysis.ListeningFormat.SampleRate);
        File.Move(path + ".part", path, overwrite: true);
    }

    /// <summary>The oldest clip still waiting to be named, with a way to remove it once named; null when none waits.</summary>
    public static (float[] Samples, DateTimeOffset Kept, Action Remove)? NextUnsorted()
    {
        if (!Directory.Exists(UnsortedDirectory)) return null;
        var path = Directory.EnumerateFiles(UnsortedDirectory, "*.wav").Order().FirstOrDefault();
        if (path is null) return null;
        try
        {
            using var stream = File.OpenRead(path);
            var kept = new DateTimeOffset(File.GetCreationTimeUtc(path), TimeSpan.Zero);
            return (WaveFile.Read(stream).Samples, kept, () => File.Delete(path));
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            File.Delete(path);
            return null;
        }
    }

    /// <summary>The noises the running night masks, in words, such as "cats and cars".</summary>
    public static string ListeningFor { get; internal set; } = "";

    /// <summary>When the running night stops by itself, or null when it runs until stopped.</summary>
    public static DateTime? StopsAt { get; set; }

    /// <summary>Technical details of the last problem that stopped a night, for a "Details" disclosure; null when none.</summary>
    public static string? ProblemDetails { get; internal set; }

    public static void Start(Context context)
    {
        Interlocked.Exchange(ref startingSince, Environment.TickCount64);
        context.StartForegroundService(new Intent(context, typeof(NightService)));
    }

    public static void Stop(Context context) =>
        context.StartService(new Intent(context, typeof(NightService)).SetAction(NightService.StopAction));

    /// <summary>What the microphone heard over the last 20 s, taken now so it can be labelled at leisure.</summary>
    public static float[]? CaptureRecent() => Engine?.Recent() is { Length: > 0 } samples ? samples : null;

    public static Recording Save(float[] samples, NoiseClass label) => ModelStore.Recordings.Save(samples, label, DateTimeOffset.Now);

    internal static void Report(NightStatus next)
    {
        var previous = Interlocked.Exchange(ref status, next);
        Interlocked.Exchange(ref startingSince, 0);
        if (next is { Running: true, Since: { } running })
        {
            // While the night runs it leaves a heartbeat, so a night Android closes can still be found and summed up.
            if (Environment.TickCount64 - Interlocked.Read(ref lastBeat) >= BeatEvery.TotalMilliseconds || next.Masked.Count != beatMasked)
            {
                Interlocked.Exchange(ref lastBeat, Environment.TickCount64);
                beatMasked = next.Masked.Count;
                Write(HeartbeatKey, new NightSummary(running, DateTimeOffset.Now, next.Masked, next.MaxBoostDb));
            }
            return;
        }
        beatMasked = -1;
        Preferences.Default.Remove(HeartbeatKey);
        // A night that ran for at least a minute is remembered when it ends.
        if (previous is { Running: true, Since: { } since } && DateTimeOffset.Now - since >= TimeSpan.FromMinutes(1))
            Write(LastNightKey, new NightSummary(since, DateTimeOffset.Now, previous.Masked, previous.MaxBoostDb));
    }

    private static void Write(string key, NightSummary night)
    {
        var lines = new List<string> { $"{Time(night.Started)} {Time(night.Ended)} {night.MaxBoostDb.ToString(CultureInfo.InvariantCulture)}" };
        lines.AddRange(night.Masked.Select(m =>
            $"{Time(m.At)} {m.Noise} {m.LoudestBoostDb.ToString(CultureInfo.InvariantCulture)}"));
        Preferences.Default.Set(key, string.Join('\n', lines));
    }

    private static string Time(DateTimeOffset at) => at.ToString("o", CultureInfo.InvariantCulture);

    private static bool TryParseTime(string text, out DateTimeOffset at) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out at);
}
