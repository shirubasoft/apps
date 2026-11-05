using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;

namespace QuietNight.Tests.Evaluation;

/// <summary>
/// How one event fared. A band of a hop needs masking when the sound dominates the room there and lifts the band above
/// what the bedtime masker covers, computed from the true room's usual peak (90th-percentile) levels. It is masked when
/// the masker's boost, as heard <c>delay</c> hops later through the earbuds, covers the excess to within
/// <see cref="ToleranceDb"/>. This is controller coverage under the assumption that the bedtime masker, as the user set
/// its volume, just covers the room's usual peaks; the acoustic result also depends on the earbuds' seal and volume.
/// </summary>
/// <param name="NeedingHops">Hops in which some band needs masking.</param>
/// <param name="MaskedAtOnce">Needing hops covered by the boost of the same hop.</param>
/// <param name="NeedingThroughBluetooth">
/// Needing hops after the first <c>delay</c> hops of the sound: no causal masker can cover the start of an unpredictable
/// sound through a delayed link, so the first onset is left out; every later onset in the sound still counts.
/// </param>
/// <param name="MaskedThroughBluetooth">Those hops covered by the boost heard through the delayed link.</param>
/// <param name="ReactionMilliseconds">From the first needing hop to the masker starting to rise; null when nothing needed masking.</param>
/// <param name="CoveredAfterMilliseconds">From the first needing hop to the first fully covered one; null when nothing needed masking.</param>
/// <param name="Recognised">The class the app reported for most of the sound's confirmed hops, or null if it never confirmed a target.</param>
/// <param name="Confirmations">Sound-model windows that rated the sound a likely target.</param>
/// <param name="BoostedShare">Share of the sound's hops with the masker noticeably boosted.</param>
/// <param name="HopsAfterFirstSecond">Hops after the sound's first second.</param>
/// <param name="BoostedAfterFirstSecond">Those hops with the masker noticeably boosted.</param>
/// <param name="ReleaseSeconds">How long after the sound ended the masker stayed noticeably boosted.</param>
/// <param name="Whole">Coverage over every needing hop, at once and through the delayed link, for the app and for the oracle controller.</param>
/// <param name="BoostedSeconds">How long the masker was noticeably boosted during the sound.</param>
/// <param name="PeakBoostDb">The largest boost during the sound.</param>
public sealed record EventScore(
    ScenarioEvent Event,
    int NeedingHops,
    int MaskedAtOnce,
    int NeedingThroughBluetooth,
    int MaskedThroughBluetooth,
    double? ReactionMilliseconds,
    double? CoveredAfterMilliseconds,
    NoiseClass? Recognised,
    int Confirmations,
    double BoostedShare,
    int HopsAfterFirstSecond,
    int BoostedAfterFirstSecond,
    double ReleaseSeconds,
    WholeCoverage Whole,
    double BoostedSeconds,
    float PeakBoostDb)
{
    public const float ToleranceDb = 1;
    public const float NoticeableBoostDb = 3;
    public static readonly int FirstSecondHops = ListeningFormat.Hops(TimeSpan.FromSeconds(1));

    public double CoverageAtOnce => NeedingHops == 0 ? 1 : (double)MaskedAtOnce / NeedingHops;

    public static EventScore Of(NightTrace trace, ScenarioEvent e, MaskerSettings settings, int bluetoothHops)
    {
        Span<float> covered = stackalloc float[MaskerBands.Count];
        settings.Covered(trace.RoomPeakDb, covered);
        var needing = 0;
        var maskedNow = 0;
        var needingLate = 0;
        var maskedLate = 0;
        int? firstNeed = null;
        int? firstRise = null;
        int? firstMasked = null;
        var end = Math.Min(e.EndHop, trace.Hops);
        for (var h = e.StartHop; h < end; h++)
        {
            var needs = false;
            var rising = false;
            var coveredNow = true;
            var coveredLate = true;
            for (var b = 0; b < MaskerBands.Count; b++)
            {
                var i = h * MaskerBands.Count + b;
                var sound = trace.SoundPower[i];
                if (sound < trace.RoomPower[i]) continue;
                var need = HopAnalyzer.ToDb(sound + trace.RoomPower[i]) - covered[b];
                if (need <= 0) continue;
                needs = true;
                rising |= trace.BoostAt(h, b) > 0;
                coveredNow &= trace.BoostAt(h, b) >= need - ToleranceDb;
                coveredLate &= trace.BoostAt(h - bluetoothHops, b) >= need - ToleranceDb;
            }
            if (!needs) continue;
            needing++;
            firstNeed ??= h;
            if (rising) firstRise ??= h;
            if (coveredNow)
            {
                maskedNow++;
                firstMasked ??= h;
            }
            if (h - firstNeed < bluetoothHops) continue;
            needingLate++;
            if (coveredLate) maskedLate++;
        }

        var confirmed = trace.Verdicts.Where(v => v.RequestHop >= e.StartHop && v.RequestHop <= e.EndHop)
            .Select(v => v.Verdict is Confirmed c ? c : null).OfType<Confirmed>().ToList();
        var reported = Enumerable.Range(e.StartHop, Math.Max(0, end - e.StartHop))
            .Select(h => trace.Masking[h]).Where(n => n.IsTarget()).ToList();
        NoiseClass? recognised = reported.Count == 0
            ? null
            : reported.GroupBy(n => n).OrderByDescending(g => g.Count()).First().Key;

        var boosted = 0;
        var later = 0;
        var boostedLater = 0;
        for (var h = e.StartHop; h < end; h++)
        {
            var isBoosted = trace.MaxBoostAt(h) > NoticeableBoostDb;
            if (isBoosted) boosted++;
            if (h - e.StartHop < FirstSecondHops) continue;
            later++;
            if (isBoosted) boostedLater++;
        }
        var released = end;
        while (released < trace.Hops && trace.MaxBoostAt(released) > NoticeableBoostDb)
            released++;
        var peak = 0f;
        for (var h = e.StartHop; h < end; h++)
            peak = MathF.Max(peak, trace.MaxBoostAt(h));

        double? Milliseconds(int? from, int? to) => from is { } f
            ? to is { } t ? (t - f) * ListeningFormat.HopSeconds * 1000 : double.PositiveInfinity
            : null;
        return new EventScore(e, needing, maskedNow, needingLate, maskedLate,
            Milliseconds(firstNeed, firstRise), Milliseconds(firstNeed, firstMasked), recognised, confirmed.Count,
            (double)boosted / Math.Max(1, end - e.StartHop), later, boostedLater,
            (released - end) * ListeningFormat.HopSeconds, WholeCoverage.Of(trace, e, covered, bluetoothHops),
            boosted * ListeningFormat.HopSeconds, peak);
    }
}

/// <summary>Needing hops of a whole sound, and how many the app's and the oracle's boosts covered, at once and through the delay.</summary>
public sealed record WholeCoverage(int Needing, int App, int AppLate, int Oracle, int OracleLate)
{
    public double AppShare => Share(App);

    public double AppLateShare => Share(AppLate);

    public double OracleShare => Share(Oracle);

    public double OracleLateShare => Share(OracleLate);

    public static WholeCoverage Of(NightTrace trace, ScenarioEvent e, ReadOnlySpan<float> covered, int delay)
    {
        int needing = 0, app = 0, appLate = 0, oracle = 0, oracleLate = 0;
        for (var h = e.StartHop; h < Math.Min(e.EndHop, trace.Hops); h++)
        {
            bool needs = false, a = true, al = true, o = true, ol = true;
            for (var b = 0; b < MaskerBands.Count; b++)
            {
                var i = h * MaskerBands.Count + b;
                if (trace.SoundPower[i] < trace.RoomPower[i]) continue;
                var need = HopAnalyzer.ToDb(trace.SoundPower[i] + trace.RoomPower[i]) - covered[b] - EventScore.ToleranceDb;
                if (need + EventScore.ToleranceDb <= 0) continue;
                needs = true;
                a &= trace.BoostAt(h, b) >= need;
                al &= trace.BoostAt(h - delay, b) >= need;
                o &= trace.OracleBoostAt(h, b) >= need;
                ol &= trace.OracleBoostAt(h - delay, b) >= need;
            }
            if (!needs) continue;
            needing++;
            if (a) app++;
            if (al) appLate++;
            if (o) oracle++;
            if (ol) oracleLate++;
        }
        return new WholeCoverage(needing, app, appLate, oracle, oracleLate);
    }

    private double Share(int part) => Needing == 0 ? 1 : (double)part / Needing;
}
