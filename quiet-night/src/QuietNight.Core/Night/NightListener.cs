using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;
using QuietNight.Core.Training;

namespace QuietNight.Core.Night;

/// <summary>
/// The whole listening path for one 10 ms hop: measure the room, ask the fast detector, update the gate and work out
/// how much each masking band must rise. It asks for a sound-model verdict every 0.24 s while something stands out, and
/// for a fresh look at the quiet room every 10 s while nothing does. The caller runs <see cref="SoundRecognizer"/> on
/// <see cref="CopyWindow"/> (on another thread in the app) and reports back with <see cref="Accept"/> or
/// <see cref="RoomLearned"/>.
/// </summary>
public sealed class NightListener
{
    private const int RingSamples = 16_384;
    private const int BriefHops = 50;
    private static readonly int RoomEveryHops = ListeningFormat.Hops(TimeSpan.FromSeconds(10));
    private static readonly int QuietBeforeRoomHops = ListeningFormat.Hops(TimeSpan.FromSeconds(1.2));
    private static readonly int RoomFallbackHops = ListeningFormat.Hops(TimeSpan.FromSeconds(3));
    private static readonly int FreezeWindowHops = ListeningFormat.Hops(TimeSpan.FromSeconds(30));
    private static readonly int FreezeBudgetHops = FreezeWindowHops * 2 / 3;

    /// <summary>How long the listener only learns the room after starting; microphones often begin with a ramp.</summary>
    public static readonly TimeSpan Settling = TimeSpan.FromSeconds(3);
    private static readonly int SettlingHops = ListeningFormat.Hops(Settling);
    private static readonly int WindowHops = SoundModel.WindowSamples / ListeningFormat.HopSamples;

    private readonly SoftmaxModel frameModel;
    private readonly HopAnalyzer analyzer = new();
    private readonly BackgroundTracker melRoom = new(ListeningFormat.MelBands);
    private readonly BackgroundTracker bandRoom = new(MaskerBands.Count);

    /// <summary>The room over the last five minutes, with its brief swells: what the bedtime masker was set to cover.</summary>
    private readonly BackgroundTracker bedtimeRoom = new(MaskerBands.Count, strideHops: 60, windowSamples: 500);
    private readonly FrameFeatures frameFeatures = new();
    private readonly float[] features = new float[FrameFeatures.Count];
    private readonly float[] probabilities = new float[NoiseClasses.Count];
    private readonly float[] rise = new float[MaskerBands.Count];

    private readonly float[] covered = new float[MaskerBands.Count];
    private readonly float[] ring = new float[RingSamples];
    private readonly int classifyHops;
    private readonly ListenerSettings settings;
    private int ringPosition;
    private bool roomKnown;
    private int activeRun;
    private readonly bool[] frozenHistory = new bool[ListeningFormat.Hops(TimeSpan.FromSeconds(30))];
    private int frozenInWindow;
    private long requestEpisode;
    private float requestFilled;
    private float requestOccupied;
    private readonly bool[] neededHistory = new bool[SoundModel.WindowSamples / ListeningFormat.HopSamples];
    private int neededInWindow;
    private long hops;
    private long lastRequest = long.MinValue / 2;
    private long lastActive = long.MinValue / 2;
    private long lastRoom = long.MinValue / 2;
    private long lastNeeded = long.MinValue / 2;
    private bool classifying;

    public NightListener(SoftmaxModel frameModel, ListenerSettings settings)
    {
        this.frameModel = frameModel;
        this.settings = settings;
        Gate = new NoiseGate(settings);
        Masker = new MaskerController(settings.Masker);
        classifyHops = ListeningFormat.Hops(TimeSpan.FromSeconds(settings.ClassifyEverySeconds));
    }

    public NoiseGate Gate { get; }

    public MaskerController Masker { get; }

    public long Hops => hops;

    /// <summary>The fast detector's target probability for the latest hop.</summary>
    public float FrameTargetProbability { get; private set; }

    /// <summary>The fast detector's input for the latest hop.</summary>
    public ReadOnlySpan<float> Features => features;

    /// <summary>How far each masking band stands above the room for the latest hop, in dB.</summary>
    public ReadOnlySpan<float> RiseDb => rise;

    /// <summary>The room's median level in each masking band, in dB relative to full scale.</summary>
    public ReadOnlySpan<float> RoomDb => bandRoom.Level;

    /// <summary>The quiet room's usual peak level in each masking band, which the bedtime masker is set to cover.</summary>
    public ReadOnlySpan<float> RoomPeakDb => bedtimeRoom.Peak;

    /// <summary>True when the latest hop wants the sound model to classify <see cref="CopyWindow"/>.</summary>
    public bool WantsVerdict { get; private set; }

    /// <summary>True when the latest hop wants the sound model to learn the quiet room from <see cref="CopyWindow"/>.</summary>
    public bool WantsRoom { get; private set; }

    /// <summary>Keeps the room level from learning the current sound, within the same freeze limit as masking.</summary>
    public bool HoldRoom { get; set; }

    /// <summary>True once the room level has been measured and rises mean something.</summary>
    public bool Warm => bandRoom.Warm && hops >= SettlingHops;

    /// <summary>True when the latest hop stood out from the room by at least <see cref="ListenerSettings.ActivityDb"/>.</summary>
    public bool Active { get; private set; }

    public void Process(ReadOnlySpan<float> hop)
    {
        WriteRing(hop);
        analyzer.Process(hop);
        var warm = bandRoom.Warm && hops >= SettlingHops;
        var loudest = float.MinValue;
        for (var b = 0; b < rise.Length; b++)
        {
            rise[b] = warm ? analyzer.BandDb[b] - bandRoom.Level[b] : 0;
            loudest = MathF.Max(loudest, rise[b]);
        }
        var active = warm && loudest >= settings.ActivityDb;
        Active = active;
        activeRun = active ? activeRun + 1 : 0;
        if (active) lastActive = hops;
        settings.Masker.Covered(bedtimeRoom.Peak, covered);
        var excess = float.MinValue;
        for (var b = 0; b < covered.Length; b++)
            excess = MathF.Max(excess, analyzer.BandDb[b] - covered[b]);
        var needed = warm && excess > 0;
        if (needed) lastNeeded = hops;
        var neededSlot = (int)(hops % neededHistory.Length);
        neededInWindow += (needed ? 1 : 0) - (neededHistory[neededSlot] ? 1 : 0);
        neededHistory[neededSlot] = needed;

        frameFeatures.Compute(analyzer.MelDb, melRoom.Level, features);
        frameModel.Predict(features, probabilities);
        FrameTargetProbability = warm ? NoiseClasses.TargetProbability(probabilities) : 0;
        Gate.Hop(FrameTargetProbability, settings.Ignored.BestTarget(probabilities), needed, features.AsSpan(0, ListeningFormat.MelBands), loudest);
        if (Gate.JustRejected) Masker.Drop();
        Masker.Update(Gate.Open && warm, analyzer.BandDb, bedtimeRoom.Peak, Gate.Confirming);

        // The room learns everything except a sound that is being masked, but never stays frozen for more than two
        // thirds of any 30 s: a sound that goes on that long has become the room, and an estimate that started too
        // low always catches up.
        var frozen = warm && (HoldRoom || (Gate.Open && active)) && frozenInWindow < FreezeBudgetHops;
        var frozenSlot = (int)(hops % FreezeWindowHops);
        frozenInWindow += (frozen ? 1 : 0) - (frozenHistory[frozenSlot] ? 1 : 0);
        frozenHistory[frozenSlot] = frozen;
        melRoom.Update(analyzer.MelDb, frozen);
        bandRoom.Update(analyzer.BandDb, frozen);
        // The bedtime cover takes in the room's brief swells, such as crickets or drips, but not sounds that stand out
        // for longer, which are events to mask or deliberately leave audible, nor anything louder than it already
        // covers: a burst of clatter early in the night would otherwise raise it for the cats that follow.
        bedtimeRoom.Update(analyzer.BandDb, frozen || activeRun > BriefHops || needed);

        // The sound model is only asked about sounds that need masking: nothing else changes what the masker does.
        var recentlyNeeded = hops - lastNeeded < classifyHops * 2;
        var ready = !classifying && warm && hops >= WindowHops;
        // The room's picture is only ever taken from a quiet window, so a disturbance never becomes the reference.
        var quiet = hops - lastActive > QuietBeforeRoomHops;
        WantsRoom = ready && quiet && (!roomKnown || hops - lastRoom >= RoomEveryHops);
        // A window taken right at a sound's onset holds only the room before it, so the first look waits until the sound
        // fills a quarter of the window.
        var soundInWindow = Gate.EpisodeHops >= classifyHops;
        WantsVerdict = ready && !WantsRoom && (roomKnown || hops >= RoomFallbackHops) && recentlyNeeded && soundInWindow && hops - lastRequest >= classifyHops;
        if (WantsVerdict)
        {
            classifying = true;
            lastRequest = hops;
            requestEpisode = Gate.Episode;
            requestFilled = (float)Gate.EpisodeHops / WindowHops;
            requestOccupied = (float)neededInWindow / WindowHops;
        }
        else if (WantsRoom)
        {
            classifying = true;
            lastRoom = hops;
        }
        hops++;
    }

    /// <summary>Copies the latest 0.96 s of audio for the sound model.</summary>
    public void CopyWindow(Span<float> window)
    {
        if (window.Length != SoundModel.WindowSamples)
            throw new ArgumentException($"A window holds {SoundModel.WindowSamples} samples.", nameof(window));
        var start = (ringPosition - SoundModel.WindowSamples + RingSamples) % RingSamples;
        var first = Math.Min(SoundModel.WindowSamples, RingSamples - start);
        ring.AsSpan(start, first).CopyTo(window);
        ring.AsSpan(0, SoundModel.WindowSamples - first).CopyTo(window[first..]);
    }

    public void Accept(SoundVerdict verdict)
    {
        classifying = false;
        if (Gate.Apply(verdict, requestEpisode, requestFilled, requestOccupied)) Masker.Drop();
    }

    /// <summary>The sound model has learned the room from the window it was given.</summary>
    public void RoomLearned()
    {
        classifying = false;
        roomKnown = true;
    }

    /// <summary>Gives up on a verdict that could not be produced, so the next one can be requested.</summary>
    public void Abandon() => classifying = false;

    private void WriteRing(ReadOnlySpan<float> hop)
    {
        var first = Math.Min(hop.Length, RingSamples - ringPosition);
        hop[..first].CopyTo(ring.AsSpan(ringPosition));
        hop[first..].CopyTo(ring);
        ringPosition = (ringPosition + hop.Length) % RingSamples;
    }
}
