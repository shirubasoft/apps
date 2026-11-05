namespace QuietNight.Core.Masking;

/// <summary>
/// How the masking noise answers a disturbing sound.
/// </summary>
/// <param name="HeadroomDb">
/// How far above the room the bedtime masker, together with the earbuds' seal, is assumed to cover before a sound
/// needs more masking.
/// </param>
/// <param name="MaxBoostDb">The loudest the masker may get above its bedtime level.</param>
/// <param name="MarginDb">Extra masking added on top of what the measured rise calls for.</param>
/// <param name="HoldSeconds">How long a band keeps its boost after the sound stops rising.</param>
/// <param name="ReleaseDbPerSecond">How quickly the boost then fades back to the bedtime level.</param>
/// <param name="AnticipationSeconds">
/// How far ahead a rising sound is extrapolated, so the masker is already loud enough when it reaches the ear. Bluetooth
/// earbuds play about a quarter of a second late, so that is the default.
/// </param>
/// <param name="DropDbPerSecond">How fast a boost falls once the sound model says it was a false alarm.</param>
/// <param name="ProvisionalHoldSeconds">How long a boost the sound model has not confirmed yet is held.</param>
/// <param name="ProvisionalReleaseDbPerSecond">How fast a boost the sound model has not confirmed yet fades.</param>
/// <param name="RunDipDb">
/// During a confirmed run each band stays within this much of the run's loudest boost, so the next meow or car is
/// already covered when it arrives, Bluetooth delay or not.
/// </param>
public sealed record MaskerSettings(
    float HeadroomDb = 6,
    float MaxBoostDb = 30,
    float MarginDb = 3,
    float HoldSeconds = 3,
    float ReleaseDbPerSecond = 3,
    float AnticipationSeconds = 0.25f,
    float DropDbPerSecond = 150,
    float ProvisionalHoldSeconds = 0.5f,
    float ProvisionalReleaseDbPerSecond = 10,
    float RunDipDb = float.PositiveInfinity)
{
    /// <summary>The most a rising sound is extrapolated ahead.</summary>
    public const float MaxAnticipationDb = 12;

    /// <summary>The bedtime masker's level in each band relative to the 500 Hz–1 kHz band: a soft brown-pink tilt.</summary>
    public static IReadOnlyList<float> ShapeDb { get; } = [4, 3, 2, 0, -2, -5, -9];

    /// <summary>
    /// What the bedtime masker already covers in each band, in microphone terms, given the room's usual peak levels. The
    /// user sets its volume so the room cannot be heard even at its usual loudest; with its fixed shape, that volume is
    /// set by the band where the room is loudest relative to the shape, and the masker then sits that far above the
    /// room's shape in every other band, plus the headroom.
    /// </summary>
    public void Covered(ReadOnlySpan<float> roomPeakDb, Span<float> coveredDb)
    {
        var anchor = float.MinValue;
        for (var b = 0; b < roomPeakDb.Length; b++)
            anchor = MathF.Max(anchor, roomPeakDb[b] - ShapeDb[b]);
        for (var b = 0; b < roomPeakDb.Length; b++)
            coveredDb[b] = anchor + ShapeDb[b] + HeadroomDb;
    }

    /// <summary>The boost a band needs when its level exceeds what the bedtime masker covers by <paramref name="excessDb"/>.</summary>
    public float Needed(float excessDb) => excessDb > 0 ? MathF.Min(excessDb + MarginDb, MaxBoostDb) : 0;
}
