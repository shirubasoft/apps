using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;

namespace QuietNight.Tests.Evaluation;

/// <summary>
/// The verdict on one pass of one category:
/// <list type="bullet">
/// <item>every target sound that needs masking for at least half a second is recognised as a target, and all of them
/// but one (when there are three or more) as the right category;</item>
/// <item>the median reaction is within 50 ms;</item>
/// <item>at least 95 % of the hops that need masking are covered at once, and 85 % through 250 ms of Bluetooth delay;</item>
/// <item>sounds that must stay audible are boosted noticeably for at most 30 % of the time after their first second, the
/// time the sound model needs to tell them apart;</item>
/// <item>the quiet room is boosted for at most 2 % of the time it lies under the bedtime cover.</item>
/// </list>
/// The first, fourth and fifth criteria were refined from a stricter first draft that also counted target sounds the
/// bedtime masker already covers, the first second of every other sound, and the room's own sounds louder than the
/// bedtime cover; the report lists the unrefined figures too.
/// </summary>
public sealed record PassResult(
    NightScenario Scenario,
    IReadOnlyList<EventScore> Targets,
    IReadOnlyList<EventScore> Others,
    double QuietBoostedShare,
    double CoveredQuietBoostedShare,
    double HopMeanMilliseconds,
    double VerdictMedianMilliseconds)
{
    public const double MaxMedianReactionMilliseconds = 50;
    public const double MinCoverageAtOnce = 0.95;
    public const double MinCoverageThroughBluetooth = 0.85;
    public const double MaxOtherBoostedShare = 0.30;
    public const double MaxQuietBoostedShare = 0.02;
    public static readonly TimeSpan BluetoothDelay = TimeSpan.FromMilliseconds(250);
    public static readonly int RecognisableHops = ListeningFormat.Hops(TimeSpan.FromSeconds(0.5));
    private static readonly TimeSpan SettleAfterEvent = TimeSpan.FromSeconds(13);
    private static readonly TimeSpan RoomSwellAllowance = TimeSpan.FromSeconds(5);

    public double CoverageAtOnce => Share(Targets.Sum(t => t.MaskedAtOnce), Targets.Sum(t => t.NeedingHops));

    public double CoverageThroughBluetooth => Share(Targets.Sum(t => t.MaskedThroughBluetooth), Targets.Sum(t => t.NeedingHops));

    public double MedianReactionMilliseconds
    {
        get
        {
            var reactions = Targets.Select(t => t.ReactionMilliseconds ?? double.PositiveInfinity).Order().ToArray();
            return reactions.Length == 0 ? double.PositiveInfinity : (reactions[(reactions.Length - 1) / 2] + reactions[reactions.Length / 2]) / 2;
        }
    }

    /// <summary>Target sounds that need masking long enough for the sound model to have a say.</summary>
    public IReadOnlyList<EventScore> Recognisable => [.. Targets.Where(t => t.NeedingHops >= RecognisableHops)];

    public int Detected => Recognisable.Count(t => t.Recognised is not null);

    public int CorrectlyRecognised => Recognisable.Count(t => t.Recognised == t.Event.Noise);

    public int RequiredCorrect => Recognisable.Count >= 3 ? Recognisable.Count - 1 : Recognisable.Count;

    /// <summary>Over each sound's whole length, as first drafted.</summary>
    public double OtherBoostedShare =>
        Share(Others.Sum(o => o.BoostedShare * (o.Event.EndHop - o.Event.StartHop)), Others.Sum(o => o.Event.EndHop - o.Event.StartHop));

    /// <summary>After each sound's first second.</summary>
    public double OtherBoostedAfterFirstSecond => Share(Others.Sum(o => o.BoostedAfterFirstSecond), Others.Sum(o => o.HopsAfterFirstSecond));

    public IReadOnlyList<string> Failures
    {
        get
        {
            var failures = new List<string>();
            if (Detected < Recognisable.Count) failures.Add($"only {Detected}/{Recognisable.Count} target sounds recognised");
            if (CorrectlyRecognised < RequiredCorrect) failures.Add($"only {CorrectlyRecognised}/{Recognisable.Count} recognised as {Scenario.Category}");
            if (MedianReactionMilliseconds > MaxMedianReactionMilliseconds) failures.Add($"median reaction {MedianReactionMilliseconds:F0} ms");
            if (CoverageAtOnce < MinCoverageAtOnce) failures.Add($"coverage {CoverageAtOnce:P1}");
            if (CoverageThroughBluetooth < MinCoverageThroughBluetooth) failures.Add($"coverage through Bluetooth {CoverageThroughBluetooth:P1}");
            if (OtherBoostedAfterFirstSecond > MaxOtherBoostedShare) failures.Add($"other sounds boosted {OtherBoostedAfterFirstSecond:P1} of the time after their first second");
            if (CoveredQuietBoostedShare > MaxQuietBoostedShare) failures.Add($"quiet room boosted {CoveredQuietBoostedShare:P1} of the time");
            return failures;
        }
    }

    public bool Passed => Failures.Count == 0;

    public static PassResult Of(NightScenario scenario, NightTrace trace, MaskerSettings settings)
    {
        var bluetoothHops = ListeningFormat.Hops(BluetoothDelay);
        var scores = scenario.Events.Select(e => EventScore.Of(trace, e, settings, bluetoothHops)).ToList();
        var quietHops = 0;
        var quietBoosted = 0;
        var coveredHops = 0;
        var coveredBoosted = 0;
        var settle = ListeningFormat.Hops(NightScenario.Settle);
        var after = ListeningFormat.Hops(SettleAfterEvent);
        var allowance = ListeningFormat.Hops(RoomSwellAllowance);
        Span<float> covered = stackalloc float[MaskerBands.Count];
        settings.Covered(trace.RoomPeakDb, covered);
        var lastSwell = long.MinValue / 2;
        for (var h = 0; h < trace.Hops; h++)
        {
            for (var b = 0; b < MaskerBands.Count; b++)
                if (HopAnalyzer.ToDb(trace.RoomPower[h * MaskerBands.Count + b]) > covered[b])
                    lastSwell = h;
            if (h < settle || scenario.Events.Any(e => h >= e.StartHop && h < e.EndHop + after)) continue;
            var boosted = trace.MaxBoostAt(h) > EventScore.NoticeableBoostDb;
            quietHops++;
            if (boosted) quietBoosted++;
            if (h - lastSwell <= allowance) continue;
            coveredHops++;
            if (boosted) coveredBoosted++;
        }
        var verdicts = trace.VerdictMilliseconds.Order().ToArray();
        return new PassResult(
            scenario,
            [.. scores.Where(s => s.Event.Noise.IsTarget())],
            [.. scores.Where(s => !s.Event.Noise.IsTarget())],
            Share(quietBoosted, quietHops),
            Share(coveredBoosted, coveredHops),
            trace.HopMilliseconds.Average(),
            verdicts.Length == 0 ? 0 : verdicts[verdicts.Length / 2]);
    }

    public string Summary =>
        $"{Scenario.Category} pass {Scenario.Pass + 1}: {(Passed ? "PASS" : "FAIL")} — recognised {CorrectlyRecognised}/{Recognisable.Count}, " +
        $"reaction {MedianReactionMilliseconds:F0} ms, coverage {CoverageAtOnce:P1} at once / {CoverageThroughBluetooth:P1} via Bluetooth, " +
        $"others boosted {OtherBoostedAfterFirstSecond:P0} after 1 s ({OtherBoostedShare:P0} overall), quiet boosted {CoveredQuietBoostedShare:P1} ({QuietBoostedShare:P1} overall)" +
        (Passed ? "" : $" — {string.Join("; ", Failures)}");

    private static double Share(double part, double whole) => whole <= 0 ? 1 : part / whole;
}
