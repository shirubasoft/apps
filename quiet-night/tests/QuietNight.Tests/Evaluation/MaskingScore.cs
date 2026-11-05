using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;

namespace QuietNight.Tests.Evaluation;

/// <summary>
/// How one event fared. A band of a hop needs masking when the sound dominates the room there and lifts the band above
/// what the bedtime masker covers, computed from the true room's usual peak (90th-percentile) levels; it is masked when the masker's boost, as heard
/// <c>delay</c> hops later through the earbuds, covers the excess to within <see cref="ToleranceDb"/>. The sound counts as
/// recognised as the class the app reports for most of its confirmed hops.
/// </summary>
public sealed record EventScore(
    ScenarioEvent Event,
    int NeedingHops,
    int MaskedAtOnce,
    int MaskedThroughBluetooth,
    double? ReactionMilliseconds,
    NoiseClass? Recognised,
    int Confirmations,
    double BoostedShare,
    int HopsAfterFirstSecond,
    int BoostedAfterFirstSecond)
{
    public const float ToleranceDb = 1;
    public const float NoticeableBoostDb = 3;
    public static readonly int FirstSecondHops = ListeningFormat.Hops(TimeSpan.FromSeconds(1));

    public static EventScore Of(NightTrace trace, ScenarioEvent e, MaskerSettings settings, int bluetoothHops)
    {
        Span<float> covered = stackalloc float[MaskerBands.Count];
        settings.Covered(trace.RoomPeakDb, covered);
        var needing = 0;
        var maskedNow = 0;
        var maskedLate = 0;
        int? firstNeed = null;
        int? firstMasked = null;
        for (var h = e.StartHop; h < Math.Min(e.EndHop, trace.Hops); h++)
        {
            var needs = false;
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
                coveredNow &= trace.BoostAt(h, b) >= need - ToleranceDb;
                coveredLate &= trace.BoostAt(h - bluetoothHops, b) >= need - ToleranceDb;
            }
            if (!needs) continue;
            needing++;
            firstNeed ??= h;
            if (coveredNow)
            {
                maskedNow++;
                firstMasked ??= h;
            }
            if (coveredLate) maskedLate++;
        }
        var verdicts = trace.Verdicts.Where(v => v.RequestHop >= e.StartHop && v.RequestHop <= e.EndHop).ToList();
        var confirmed = verdicts.Select(v => v.Verdict is Confirmed c ? c : null).OfType<Confirmed>().ToList();
        var reported = Enumerable.Range(e.StartHop, Math.Max(0, Math.Min(e.EndHop, trace.Hops) - e.StartHop))
            .Select(h => trace.Masking[h]).Where(n => n.IsTarget()).ToList();
        NoiseClass? recognised = confirmed.Count == 0 || reported.Count == 0
            ? null
            : reported.GroupBy(n => n).OrderByDescending(g => g.Count()).First().Key;
        var boosted = 0;
        var later = 0;
        var boostedLater = 0;
        for (var h = e.StartHop; h < Math.Min(e.EndHop, trace.Hops); h++)
        {
            var isBoosted = trace.MaxBoostAt(h) > NoticeableBoostDb;
            if (isBoosted) boosted++;
            if (h - e.StartHop < FirstSecondHops) continue;
            later++;
            if (isBoosted) boostedLater++;
        }
        double? reaction = firstNeed is { } need0 && firstMasked is { } masked0 ? (masked0 - need0) * ListeningFormat.HopSeconds * 1000 : null;
        return new EventScore(e, needing, maskedNow, maskedLate, reaction, recognised, confirmed.Count,
            (double)boosted / Math.Max(1, e.EndHop - e.StartHop), later, boostedLater);
    }
}
