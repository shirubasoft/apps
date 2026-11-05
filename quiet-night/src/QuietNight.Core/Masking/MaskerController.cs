using QuietNight.Core.Analysis;

namespace QuietNight.Core.Masking;

/// <summary>
/// Raises each masking band as soon as a disturbing sound needs it, holds it, then fades it slowly so the masker never
/// pumps with the sound.
/// </summary>
public sealed class MaskerController(MaskerSettings settings)
{
    private readonly float[] boost = new float[MaskerBands.Count];
    private readonly long[] holdUntil = new long[MaskerBands.Count];
    private readonly int holdHops = ListeningFormat.Hops(TimeSpan.FromSeconds(settings.HoldSeconds));
    private readonly float releasePerHop = settings.ReleaseDbPerSecond * ListeningFormat.HopSeconds;
    private readonly int provisionalHoldHops = ListeningFormat.Hops(TimeSpan.FromSeconds(settings.ProvisionalHoldSeconds));
    private readonly float provisionalReleasePerHop = settings.ProvisionalReleaseDbPerSecond * ListeningFormat.HopSeconds;
    private readonly float dropPerHop = settings.DropDbPerSecond * ListeningFormat.HopSeconds;
    private readonly bool[] dropping = new bool[MaskerBands.Count];
    private int hop;

    public MaskerSettings Settings { get; } = settings;

    /// <summary>The boost of each band above the bedtime level, in dB.</summary>
    public ReadOnlySpan<float> BoostDb => boost;

    public float LoudestBoostDb => boost.Max();

    private readonly float[] covered = new float[MaskerBands.Count];
    private readonly bool[] gentle = new bool[MaskerBands.Count];
    private readonly float[] runPeak = new float[MaskerBands.Count];

    private readonly float[][] recent = [.. Enumerable.Range(0, 2 * SlopeHops + 1).Select(_ => new float[MaskerBands.Count])];
    private const int SlopeHops = 10;
    private const float SustainedRiseDb = 1;


    /// <summary>Lets every boost fall quickly, without its hold, after a false alarm.</summary>
    public void Drop()
    {
        for (var b = 0; b < boost.Length; b++)
        {
            holdUntil[b] = hop;
            dropping[b] = boost[b] > 0;
        }
    }

    /// <param name="disturbing">Whether the gate says the current sound should be masked.</param>
    /// <param name="levelDb">The microphone's level in each band.</param>
    /// <param name="roomPeakDb">The room's usual peak level in each band.</param>
    /// <param name="confirmed">
    /// Whether the sound model has confirmed a target. Until then boosts are held for
    /// <see cref="MaskerSettings.ProvisionalHoldSeconds"/> and fade quickly; once it has,
    /// they hold for <see cref="MaskerSettings.HoldSeconds"/> and fade gently so the masker never pumps.
    /// </param>
    public void Update(bool disturbing, ReadOnlySpan<float> levelDb, ReadOnlySpan<float> roomPeakDb, bool confirmed = false)
    {
        Settings.Covered(roomPeakDb, covered);
        var newest = recent[hop % recent.Length];
        var middle = recent[(hop + SlopeHops + 1) % recent.Length];
        var oldest = recent[(hop + 1) % recent.Length];
        levelDb.CopyTo(newest);
        var lookahead = Settings.AnticipationSeconds / (SlopeHops * ListeningFormat.HopSeconds);
        for (var b = 0; b < boost.Length; b++)
        {
            // A sound that already needs masking and has kept rising for 200 ms, like an approaching engine, is met where
            // it will be when the masker is heard. A sudden onset that levels off is not extrapolated.
            var sustained = hop >= 2 * SlopeHops && newest[b] - middle[b] >= SustainedRiseDb && middle[b] - oldest[b] >= SustainedRiseDb;
            var rising = sustained ? (newest[b] - middle[b]) : 0;
            var expected = levelDb[b] + MathF.Min(rising * lookahead, MaskerSettings.MaxAnticipationDb);
            var needed = disturbing && levelDb[b] > covered[b] ? Settings.Needed(expected - covered[b]) : 0;
            runPeak[b] = confirmed ? MathF.Max(runPeak[b], needed) : 0;
            var target = MathF.Max(needed, runPeak[b] - Settings.RunDipDb);
            if (target > 0 && target >= boost[b])
            {
                boost[b] = target;
                holdUntil[b] = hop + (confirmed ? holdHops : provisionalHoldHops);
                dropping[b] = false;
                gentle[b] = confirmed;
            }
            else if (confirmed && target > 0)
            {
                gentle[b] = true;
            }
            if (hop >= holdUntil[b] && target < boost[b])
            {
                var fall = dropping[b] ? dropPerHop : gentle[b] ? releasePerHop : provisionalReleasePerHop;
                boost[b] = MathF.Max(target, boost[b] - fall);
            }
        }
        hop++;
    }
}
