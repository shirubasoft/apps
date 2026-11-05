using System.Diagnostics;
using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;
using QuietNight.Core.Night;
using QuietNight.Core.Training;

namespace QuietNight.Tests.Evaluation;

public sealed record TimedVerdict(long RequestHop, SoundVerdict Verdict);

/// <summary>
/// Everything the app did during a scenario, hop by hop, next to the ground truth: the power of the sounds alone and of
/// the room alone in each masking band.
/// </summary>
public sealed class NightTrace
{
    private NightTrace(int hops)
    {
        Hops = hops;
        Boost = new float[hops * MaskerBands.Count];
        SoundPower = new float[hops * MaskerBands.Count];
        RoomPower = new float[hops * MaskerBands.Count];
        GateOpen = new bool[hops];
        Masking = new NoiseClass[hops];
        MixtureDb = new float[hops * MaskerBands.Count];
        OracleBoost = new float[hops * MaskerBands.Count];
    }

    /// <summary>The microphone's level in each band, as any controller would measure it.</summary>
    public float[] MixtureDb { get; }

    /// <summary>
    /// The boosts of an oracle controller: the same masker, delay, limits and release, but told exactly when a target
    /// sound is present and how loud the room really is. What it cannot cover, no causal controller could.
    /// </summary>
    public float[] OracleBoost { get; }

    /// <summary>
    /// How long a sound-model verdict takes to come back: 150 ms, a conservative allowance for one mid-range phone core
    /// running YAMNet, the hand-off between threads and scheduling.
    /// </summary>
    public const int VerdictDelayHops = 15;

    public int Hops { get; }

    public float[] Boost { get; }

    public float[] SoundPower { get; }

    public float[] RoomPower { get; }

    public float[] RoomMeanPower { get; } = new float[MaskerBands.Count];

    /// <summary>The true room's 90th-percentile level in each band, in dB: what the bedtime masker is set to cover.</summary>
    public float[] RoomPeakDb { get; } = new float[MaskerBands.Count];

    public bool[] GateOpen { get; }

    /// <summary>The noise the app reports for each hop while a confirmed run is under way, else <see cref="NoiseClass.Other"/>.</summary>
    public NoiseClass[] Masking { get; }

    public List<TimedVerdict> Verdicts { get; } = [];

    /// <summary>Wall-clock time per hop of the listening path, excluding the sound model.</summary>
    public List<double> HopMilliseconds { get; } = [];

    /// <summary>Wall-clock time of each sound model call.</summary>
    public List<double> VerdictMilliseconds { get; } = [];

    /// <summary>
    /// Runs the app's listening path over the scenario. Each verdict is computed on the window the listener asked for
    /// and handed back <paramref name="verdictDelayHops"/> hops later, as the app's background thread would.
    /// </summary>
    public static NightTrace Record(NightScenario scenario, DetectorHeads heads, SoundModel model, ListenerSettings settings, int verdictDelayHops = VerdictDelayHops)
    {
        var recognizer = new SoundRecognizer(model, heads.Sound, settings);
        return Record(scenario, heads, settings, (_, window) => recognizer.Classify(window), (_, window) => recognizer.LearnRoom(window), verdictDelayHops);
    }

    /// <summary>Runs the listening path with any sound model, for example one that reuses features across runs.</summary>
    public static NightTrace Record(NightScenario scenario, DetectorHeads heads, ListenerSettings settings,
        Func<int, float[], SoundVerdict> classify, Action<int, float[]> learnRoom, int verdictDelayHops = VerdictDelayHops)
    {
        var trace = new NightTrace(scenario.Hops);
        var listener = new NightListener(heads.Frame, settings);
        var soundAlone = new HopAnalyzer();
        var roomAlone = new HopAnalyzer();
        var mixtureAlone = new HopAnalyzer();
        var mixture = scenario.Mixture();
        var window = new float[SoundModel.WindowSamples];
        (long Due, long Requested, SoundVerdict Verdict)? pending = null;
        long? roomDue = null;
        var clock = new Stopwatch();
        for (var h = 0; h < scenario.Hops; h++)
        {
            if (pending is { } due && due.Due <= h)
            {
                listener.Accept(due.Verdict);
                trace.Verdicts.Add(new TimedVerdict(due.Requested, due.Verdict));
                pending = null;
            }
            if (roomDue is { } learned && learned <= h)
            {
                listener.RoomLearned();
                roomDue = null;
            }
            var range = new Range(h * ListeningFormat.HopSamples, (h + 1) * ListeningFormat.HopSamples);
            clock.Restart();
            listener.Process(mixture.AsSpan(range));
            trace.HopMilliseconds.Add(clock.Elapsed.TotalMilliseconds);
            if (listener.WantsVerdict)
            {
                listener.CopyWindow(window);
                clock.Restart();
                var verdict = classify(h, window);
                trace.VerdictMilliseconds.Add(clock.Elapsed.TotalMilliseconds);
                pending = (h + verdictDelayHops, h, verdict);
            }
            else if (listener.WantsRoom)
            {
                listener.CopyWindow(window);
                learnRoom(h, window);
                roomDue = h + verdictDelayHops;
            }
            soundAlone.Process(scenario.Sounds.AsSpan(range));
            roomAlone.Process(scenario.Room.AsSpan(range));
            mixtureAlone.Process(mixture.AsSpan(range));
            mixtureAlone.BandDb.CopyTo(trace.MixtureDb.AsSpan(h * MaskerBands.Count));
            listener.Masker.BoostDb.CopyTo(trace.Boost.AsSpan(h * MaskerBands.Count));
            soundAlone.BandPower.CopyTo(trace.SoundPower.AsSpan(h * MaskerBands.Count));
            roomAlone.BandPower.CopyTo(trace.RoomPower.AsSpan(h * MaskerBands.Count));
            trace.GateOpen[h] = listener.Gate.Open;
            trace.Masking[h] = listener.Gate.Confirming ? listener.Gate.Noise : NoiseClass.Other;
            for (var b = 0; b < MaskerBands.Count; b++)
                trace.RoomMeanPower[b] += roomAlone.BandPower[b] / scenario.Hops;
        }
        for (var b = 0; b < MaskerBands.Count; b++)
        {
            var levels = Enumerable.Range(0, trace.Hops).Select(h => HopAnalyzer.ToDb(trace.RoomPower[h * MaskerBands.Count + b])).Order().ToArray();
            trace.RoomPeakDb[b] = levels[(int)(levels.Length * 0.9)];
        }
        var oracle = new MaskerController(settings.Masker);
        for (var h = 0; h < trace.Hops; h++)
        {
            var target = scenario.Events.Any(e => e.Noise.IsTarget() && h >= e.StartHop && h < e.EndHop);
            oracle.Update(target, trace.MixtureDb.AsSpan(h * MaskerBands.Count, MaskerBands.Count), trace.RoomPeakDb, confirmed: true);
            oracle.BoostDb.CopyTo(trace.OracleBoost.AsSpan(h * MaskerBands.Count));
        }
        return trace;
    }

    public float BoostAt(int hop, int band) => hop < 0 ? 0 : Boost[hop * MaskerBands.Count + band];

    public float OracleBoostAt(int hop, int band) => hop < 0 ? 0 : OracleBoost[hop * MaskerBands.Count + band];

    public float MaxBoostAt(int hop)
    {
        var max = 0f;
        for (var b = 0; b < MaskerBands.Count; b++)
            max = MathF.Max(max, BoostAt(hop, b));
        return max;
    }
}
