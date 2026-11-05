using System.Collections.Concurrent;
using Android.Content;
using Android.Media;
using QuietNight.Audio;
using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;
using QuietNight.Core.Night;
using QuietNight.Core.Training;
using QuietNight.Masking;

namespace QuietNight.Night;

/// <summary>
/// Runs the night: a high-priority thread reads the microphone every 10 ms and drives the listener and the masker,
/// while a second thread runs the sound model whenever the listener asks for a verdict.
/// </summary>
public sealed class NightEngine : IDisposable
{
    public static readonly TimeSpan RecentLength = TimeSpan.FromSeconds(20);
    private const int PublishEveryHops = 10;

    private readonly Context context;
    private readonly NightListener listener;
    private readonly SoundRecognizer recognizer;
    private readonly SoundModel model;
    private readonly MaskerPlayer masker;
    private readonly float[] window = new float[SoundModel.WindowSamples];
    private readonly float[] recent = new float[(int)(RecentLength.TotalSeconds * ListeningFormat.SampleRate)];
    private readonly ConcurrentQueue<ModelOutcome> outcomes = new();
    private readonly SemaphoreSlim windowReady = new(0, 1);
    private readonly List<MaskedNoise> masked = [];
    private readonly Thread capture;
    private readonly Thread classify;
    private readonly DateTimeOffset since = DateTimeOffset.Now;
    private readonly Lock recentLock = new();
    private AudioRecord? record;
    private int recentPosition;
    private bool recentFull;
    private volatile bool running;
    private bool inEpisode;
    private volatile bool learnRoom;
    private float[] bedtime = new float[MaskerBands.Count];

    public NightEngine(Context context, NightSettings settings, DetectorHeads heads, byte[] yamnet, float[][] layers)
    {
        this.context = context;
        SetBedtime(settings);
        var listenerSettings = settings.Listener;
        listener = new NightListener(heads.Frame, listenerSettings);
        model = new SoundModel(yamnet);
        recognizer = new SoundRecognizer(model, heads.Sound, listenerSettings);
        masker = new MaskerPlayer(layers);
        capture = new Thread(Capture) { IsBackground = true, Name = "Night capture" };
        classify = new Thread(Classify) { IsBackground = true, Name = "Night sound model" };
    }

    public event Action<NightStatus>? Published;

    public bool Muted
    {
        get => masker.Muted;
        set => masker.Muted = value;
    }

    public void Start()
    {
        record = Microphone.Open(context);
        masker.SetLevels(Volatile.Read(ref bedtime));
        masker.Start();
        running = true;
        record.StartRecording();
        capture.Start();
        classify.Start();
    }

    /// <summary>Applies a new masker volume at once; other settings take effect on the next start.</summary>
    public void SetBedtime(NightSettings settings)
    {
        var levels = new float[MaskerBands.Count];
        settings.BedtimeLevels(levels);
        Volatile.Write(ref bedtime, levels);
    }

    /// <summary>The last 20 s the microphone heard, oldest first, so a sound that got through can be saved.</summary>
    public float[] Recent()
    {
        lock (recentLock)
        {
            if (!recentFull) return recent[..recentPosition];
            return [.. recent[recentPosition..], .. recent[..recentPosition]];
        }
    }

    public void Dispose()
    {
        running = false;
        try
        {
            record?.Stop();
        }
        catch (Java.Lang.IllegalStateException)
        {
        }
        if (capture.IsAlive) capture.Join(TimeSpan.FromSeconds(2));
        windowReady.Release();
        if (classify.IsAlive) classify.Join(TimeSpan.FromSeconds(2));
        record?.Release();
        record?.Dispose();
        masker.Dispose();
        model.Dispose();
    }

    private void Capture()
    {
        Android.OS.Process.SetThreadPriority(Android.OS.ThreadPriority.UrgentAudio);
        var hop = new float[ListeningFormat.HopSamples];
        Span<float> levels = stackalloc float[MaskerBands.Count];
        var problem = (string?)null;
        while (running)
        {
            var filled = 0;
            while (filled < hop.Length && running)
            {
                var read = record!.Read(hop, filled, hop.Length - filled, 0);
                if (read <= 0)
                {
                    problem = $"The microphone stopped delivering audio ({read}).";
                    running = false;
                    break;
                }
                filled += read;
            }
            if (!running) break;
            Remember(hop);
            while (outcomes.TryDequeue(out var outcome))
            {
                switch (outcome)
                {
                    case Heard heard:
                        listener.Accept(heard.Verdict);
                        break;
                    case RoomLearned:
                        listener.RoomLearned();
                        break;
                    default:
                        listener.Abandon();
                        break;
                }
            }
            listener.Process(hop);
            if (listener.WantsVerdict || listener.WantsRoom)
            {
                listener.CopyWindow(window);
                learnRoom = listener.WantsRoom;
                windowReady.Release();
            }
            var boost = listener.Masker.BoostDb;
            var bed = Volatile.Read(ref bedtime);
            for (var b = 0; b < levels.Length; b++)
                levels[b] = bed[b] + boost[b];
            masker.SetLevels(levels);
            Track();
            if (listener.Hops % PublishEveryHops == 0)
                Publish(null);
        }
        Publish(problem);
    }

    private void Classify()
    {
        while (true)
        {
            windowReady.Wait();
            if (!running) return;
            try
            {
                if (learnRoom)
                {
                    recognizer.LearnRoom(window);
                    outcomes.Enqueue(new RoomLearned());
                }
                else
                {
                    outcomes.Enqueue(new Heard(recognizer.Classify(window)));
                }
            }
            catch (Exception e) when (e is Microsoft.ML.OnnxRuntime.OnnxRuntimeException or InvalidOperationException)
            {
                Android.Util.Log.Warn("QuietNight", $"Sound model failed: {e.Message}");
                outcomes.Enqueue(new Failed(e.Message));
            }
        }
    }

    private void Remember(float[] hop)
    {
        lock (recentLock)
        {
            hop.CopyTo(recent, recentPosition);
            recentPosition += hop.Length;
            if (recentPosition < recent.Length) return;
            recentPosition = 0;
            recentFull = true;
        }
    }

    /// <summary>Logs each run of confirmed target noise once, with the largest boost it needed.</summary>
    private void Track()
    {
        var confirming = listener.Gate.Confirming && listener.Gate.Noise.IsTarget();
        if (!confirming)
        {
            inEpisode = false;
            return;
        }
        lock (masked)
        {
            if (!inEpisode) masked.Add(new MaskedNoise(listener.Gate.Noise, DateTimeOffset.Now, listener.Masker.LoudestBoostDb));
            else masked[^1] = masked[^1] with { LoudestBoostDb = MathF.Max(masked[^1].LoudestBoostDb, listener.Masker.LoudestBoostDb) };
        }
        inEpisode = true;
    }

    private void Publish(string? problem)
    {
        var roomDb = 10 * MathF.Log10(listener.RoomDb.ToArray().Sum(db => MathF.Pow(10, db / 10)) + 1e-12f);
        MaskedNoise[] snapshot;
        lock (masked)
            snapshot = [.. masked];
        Published?.Invoke(new NightStatus(
            problem is null,
            since,
            listener.Masker.LoudestBoostDb > 0 ? listener.Gate.Noise : NoiseClass.Other,
            listener.Masker.LoudestBoostDb,
            listener.Warm ? roomDb : float.NaN,
            snapshot,
            problem));
    }
}

/// <summary>The sound model classified a window.</summary>
public sealed record class Heard(SoundVerdict Verdict);

/// <summary>The sound model learned the quiet room from a window.</summary>
public sealed record class RoomLearned;

/// <summary>The sound model could not run.</summary>
public sealed record class Failed(string Reason);

/// <summary>What the sound model thread hands back to the capture thread.</summary>
public union ModelOutcome(Heard, RoomLearned, Failed);
