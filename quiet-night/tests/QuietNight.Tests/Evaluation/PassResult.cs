using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;

namespace QuietNight.Tests.Evaluation;

/// <summary>
/// The verdict on one pass of one category under acceptance criteria version 3:
/// <list type="number">
/// <item>every target sound that needs masking for at least half a second is recognised as a target, and all of them
/// but one (when there are three or more) as the right category;</item>
/// <item>the median controller reaction, from a sound first needing masking to the masker starting to rise, is within
/// 50 ms;</item>
/// <item>over the whole of the target sounds, coverage at once is within 5 points of an oracle controller's, and
/// coverage through 250 ms of Bluetooth delay within 10 points of the oracle's; the oracle has the same masker, delay,
/// boost limit and release, but is told exactly when a target is present and how loud the room is, so it marks what no
/// causal controller could cover;</item>
/// <item>every target sound that needs masking at all, however briefly, is within 15 points of the oracle at once;</item>
/// <item>no sound that must stay audible is noticeably boosted for more than one second in total;</item>
/// <item>the masker is noticeably boosted for at most 2 % of the quiet night, counting false alarms' tails and the
/// room's own swells, and leaving out only true target sounds and the time the oracle's boost for them takes to settle.</item>
/// </list>
/// Version 2 differed only in judging the worst sound among those needing masking for half a second, and in leaving out
/// a fixed 13 s after each target. Version 1 (absolute 95 % and 85 % coverage, protected sounds judged after their first
/// second, and quiet time excluding every event's tail and the room's swells) is reported too.
/// </summary>
public sealed record PassResult(
    NightScenario Scenario,
    IReadOnlyList<EventScore> Targets,
    IReadOnlyList<EventScore> Others,
    double QuietBoostedShare,
    double CoveredQuietBoostedShare,
    double NightQuietBoostedShare,
    double NightQuietBoostedShareVersion2,
    double NightBoostedShare,
    double HopMeanMilliseconds,
    double VerdictMedianMilliseconds)
{
    public const double MaxMedianReactionMilliseconds = 50;
    public const double MaxOracleGapAtOnce = 0.05;
    public const double MaxOracleGapThroughBluetooth = 0.10;
    public const double MaxWorstEventOracleGap = 0.15;
    public const double MaxProtectedBoostedSeconds = 1.0;
    public const double MaxQuietBoostedShare = 0.02;
    public static readonly TimeSpan BluetoothDelay = TimeSpan.FromMilliseconds(250);
    public static readonly int RecognisableHops = ListeningFormat.Hops(TimeSpan.FromSeconds(0.5));
    private static readonly TimeSpan SettleAfterEvent = TimeSpan.FromSeconds(13);
    private static readonly TimeSpan RoomSwellAllowance = TimeSpan.FromSeconds(5);

    /// <summary>Target sounds that need masking long enough for the sound model to have a say.</summary>
    public IReadOnlyList<EventScore> Recognisable => [.. Targets.Where(t => t.NeedingHops >= RecognisableHops)];

    public int Detected => Recognisable.Count(t => t.Recognised is not null);

    public int CorrectlyRecognised => Recognisable.Count(t => t.Recognised == t.Event.Noise);

    public int RequiredCorrect => Recognisable.Count >= 3 ? Recognisable.Count - 1 : Recognisable.Count;

    /// <summary>The median reaction over target sounds that needed masking; zero when none did.</summary>
    public double MedianReactionMilliseconds
    {
        get
        {
            var reactions = Targets.Select(t => t.ReactionMilliseconds).OfType<double>().Order().ToArray();
            return reactions.Length == 0 ? 0 : (reactions[(reactions.Length - 1) / 2] + reactions[reactions.Length / 2]) / 2;
        }
    }

    public double WholeAtOnce => Covered(Targets.Sum(t => t.Whole.App), Targets.Sum(t => t.Whole.Needing));

    public double OracleAtOnce => Covered(Targets.Sum(t => t.Whole.Oracle), Targets.Sum(t => t.Whole.Needing));

    public double WholeThroughBluetooth => Covered(Targets.Sum(t => t.Whole.AppLate), Targets.Sum(t => t.Whole.Needing));

    public double OracleThroughBluetooth => Covered(Targets.Sum(t => t.Whole.OracleLate), Targets.Sum(t => t.Whole.Needing));

    /// <summary>The target sound whose coverage falls furthest below the oracle's.</summary>
    public double WorstEventOracleGap => Targets.Where(t => t.Whole.Needing > 0).Select(t => t.Whole.OracleShare - t.Whole.AppShare).DefaultIfEmpty(0).Max();

    /// <summary>Version 2's worst sound, among those needing masking for half a second.</summary>
    public double WorstRecognisableOracleGap => Recognisable.Select(t => t.Whole.OracleShare - t.Whole.AppShare).DefaultIfEmpty(0).Max();

    public double LongestProtectedBoostSeconds => Others.Select(o => o.BoostedSeconds).DefaultIfEmpty(0).Max();

    public double MeanReleaseSeconds => Targets.Select(t => t.ReleaseSeconds).DefaultIfEmpty(0).Average();

    // Version 1 figures, reported for comparison.
    public double CoverageAtOnce => Covered(Targets.Sum(t => t.MaskedAtOnce), Targets.Sum(t => t.NeedingHops));

    public double CoverageThroughBluetooth => Covered(Targets.Sum(t => t.MaskedThroughBluetooth), Targets.Sum(t => t.NeedingThroughBluetooth));

    public double OtherBoostedShare =>
        Boosted(Others.Sum(o => o.BoostedShare * (o.Event.EndHop - o.Event.StartHop)), Others.Sum(o => o.Event.EndHop - o.Event.StartHop));

    public double OtherBoostedAfterFirstSecond => Boosted(Others.Sum(o => o.BoostedAfterFirstSecond), Others.Sum(o => o.HopsAfterFirstSecond));

    public bool PassedVersion2 =>
        Detected == Recognisable.Count && CorrectlyRecognised >= RequiredCorrect && MedianReactionMilliseconds <= MaxMedianReactionMilliseconds &&
        WholeAtOnce >= OracleAtOnce - MaxOracleGapAtOnce && WholeThroughBluetooth >= OracleThroughBluetooth - MaxOracleGapThroughBluetooth &&
        WorstRecognisableOracleGap <= MaxWorstEventOracleGap && LongestProtectedBoostSeconds <= MaxProtectedBoostedSeconds &&
        NightQuietBoostedShareVersion2 <= MaxQuietBoostedShare;

    public bool PassedVersion1 =>
        Detected == Recognisable.Count && CorrectlyRecognised >= RequiredCorrect && MedianReactionMilliseconds <= MaxMedianReactionMilliseconds &&
        CoverageAtOnce >= 0.95 && CoverageThroughBluetooth >= 0.85 && OtherBoostedAfterFirstSecond <= 0.30 && CoveredQuietBoostedShare <= MaxQuietBoostedShare;

    public IReadOnlyList<string> Failures
    {
        get
        {
            var failures = new List<string>();
            if (Detected < Recognisable.Count) failures.Add($"only {Detected}/{Recognisable.Count} target sounds recognised");
            if (CorrectlyRecognised < RequiredCorrect) failures.Add($"only {CorrectlyRecognised}/{Recognisable.Count} recognised as {Scenario.Category}");
            if (MedianReactionMilliseconds > MaxMedianReactionMilliseconds) failures.Add($"median reaction {MedianReactionMilliseconds:F0} ms");
            if (WholeAtOnce < OracleAtOnce - MaxOracleGapAtOnce) failures.Add($"coverage {WholeAtOnce:P1} against the oracle's {OracleAtOnce:P1}");
            if (WholeThroughBluetooth < OracleThroughBluetooth - MaxOracleGapThroughBluetooth)
                failures.Add($"Bluetooth coverage {WholeThroughBluetooth:P1} against the oracle's {OracleThroughBluetooth:P1}");
            if (WorstEventOracleGap > MaxWorstEventOracleGap) failures.Add($"worst sound {WorstEventOracleGap:P0} below the oracle");
            if (LongestProtectedBoostSeconds > MaxProtectedBoostedSeconds) failures.Add($"a protected sound boosted for {LongestProtectedBoostSeconds:F1} s");
            if (NightQuietBoostedShare > MaxQuietBoostedShare) failures.Add($"quiet night boosted {NightQuietBoostedShare:P1} of the time");
            return failures;
        }
    }

    public bool Passed => Failures.Count == 0;

    public static PassResult Of(NightScenario scenario, NightTrace trace, MaskerSettings settings)
    {
        var bluetoothHops = ListeningFormat.Hops(BluetoothDelay);
        var scores = scenario.Events.Select(e => EventScore.Of(trace, e, settings, bluetoothHops)).ToList();
        int quietHops = 0, quietBoosted = 0, coveredHops = 0, coveredBoosted = 0, nightQuietHops = 0, nightQuietBoosted = 0, nightHops = 0, nightBoosted = 0;
        int referenceQuietHops = 0, referenceQuietBoosted = 0;
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
            if (h < settle) continue;
            var boosted = trace.MaxBoostAt(h) > EventScore.NoticeableBoostDb;
            nightHops++;
            if (boosted) nightBoosted++;
            var inSound = scenario.Events.Any(e => h >= e.StartHop && h < e.EndHop);
            var afterTarget = scenario.Events.Any(e => e.Noise.IsTarget() && h >= e.StartHop && h < e.EndHop + after);
            if (!inSound && !afterTarget)
            {
                nightQuietHops++;
                if (boosted) nightQuietBoosted++;
            }
            if (!inSound && trace.MaxOracleBoostAt(h) <= EventScore.NoticeableBoostDb)
            {
                referenceQuietHops++;
                if (boosted) referenceQuietBoosted++;
            }
            if (scenario.Events.Any(e => h >= e.StartHop && h < e.EndHop + after)) continue;
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
            Boosted(quietBoosted, quietHops),
            Boosted(coveredBoosted, coveredHops),
            Boosted(referenceQuietBoosted, referenceQuietHops),
            Boosted(nightQuietBoosted, nightQuietHops),
            Boosted(nightBoosted, nightHops),
            trace.HopMilliseconds.Average(),
            verdicts.Length == 0 ? 0 : verdicts[verdicts.Length / 2]);
    }

    public string Summary =>
        $"{Scenario.Category} pass {Scenario.Pass + 1}: {(Passed ? "PASS" : "FAIL")} (v2 {(PassedVersion2 ? "pass" : "fail")}, v1 {(PassedVersion1 ? "pass" : "fail")}) — recognised {CorrectlyRecognised}/{Recognisable.Count}, " +
        $"reaction {MedianReactionMilliseconds:F0} ms, coverage {WholeAtOnce:P1}/{OracleAtOnce:P1} oracle at once, {WholeThroughBluetooth:P1}/{OracleThroughBluetooth:P1} via Bluetooth, " +
        $"worst gap {WorstEventOracleGap:P0}, protected boost ≤ {LongestProtectedBoostSeconds:F1} s, quiet night {NightQuietBoostedShare:P1}" +
        (Passed ? "" : $" — {string.Join("; ", Failures)}");

    /// <summary>A coverage share: with nothing to cover, everything is covered.</summary>
    private static double Covered(double part, double whole) => whole <= 0 ? 1 : part / whole;

    /// <summary>A boosted share: with nothing to measure, nothing was boosted.</summary>
    private static double Boosted(double part, double whole) => whole <= 0 ? 0 : part / whole;
}
