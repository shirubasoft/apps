namespace QuietNight.Core.Analysis;

/// <summary>
/// The room's level in each band from a running histogram of recent levels: its median and its usual peak, the 90th
/// percentile. By default it spans the last 30 s, sampled every 50 ms. The caller freezes it while a sound is being
/// masked, so a car idling outside never becomes part of the room, while a new steady sound that is not masked, such as
/// rain or a fan, becomes the room within seconds.
/// </summary>
public sealed class BackgroundTracker
{
    public const int WarmupHops = 50;
    private const float LowestDb = -130;
    private const float StepDb = 0.5f;
    private const int Bins = 280;

    private readonly int bands;
    private readonly int strideHops;
    private readonly int windowSamples;
    private readonly short[] ring;
    private readonly int[] histogram;
    private readonly float[] level;
    private readonly float[] peak;
    private int count;
    private int position;
    private long hops;

    /// <param name="bands">How many bands to follow.</param>
    /// <param name="strideHops">How many hops apart the levels are sampled.</param>
    /// <param name="windowSamples">How many samples the histogram keeps.</param>
    public BackgroundTracker(int bands, int strideHops = 5, int windowSamples = 600)
    {
        this.bands = bands;
        this.strideHops = strideHops;
        this.windowSamples = windowSamples;
        ring = new short[bands * windowSamples];
        histogram = new int[bands * Bins];
        level = new float[bands];
        peak = new float[bands];
    }

    public ReadOnlySpan<float> Level => level;

    /// <summary>The level each band reaches or exceeds 10 % of the time: the room at its usual loudest.</summary>
    public ReadOnlySpan<float> Peak => peak;

    public bool Warm => hops >= WarmupHops;

    public void Update(ReadOnlySpan<float> db, bool frozen)
    {
        // Until the first sample, follow the input so early rises read as zero; a median of samples afterwards
        // shrugs off the silence a microphone often starts with.
        if (count == 0)
            for (var b = 0; b < bands; b++)
                level[b] = peak[b] = db[b];
        if ((!frozen || count == 0) && hops % strideHops == 0)
            Push(db);
        hops++;
    }

    private void Push(ReadOnlySpan<float> db)
    {
        var full = count == windowSamples;
        for (var b = 0; b < bands; b++)
        {
            var bin = (short)Math.Clamp((int)MathF.Round((db[b] - LowestDb) / StepDb), 0, Bins - 1);
            if (full) histogram[b * Bins + ring[b * windowSamples + position]]--;
            ring[b * windowSamples + position] = bin;
            histogram[b * Bins + bin]++;
        }
        position = (position + 1) % windowSamples;
        if (!full) count++;
        for (var b = 0; b < bands; b++)
        {
            level[b] = Percentile(b, 0.5);
            peak[b] = Percentile(b, 0.9);
        }
    }

    private float Percentile(int band, double share)
    {
        var wanted = Math.Max(1, (int)Math.Ceiling(count * share));
        var seen = 0;
        for (var bin = 0; bin < Bins; bin++)
        {
            seen += histogram[band * Bins + bin];
            if (seen >= wanted) return LowestDb + bin * StepDb;
        }
        return LowestDb + (Bins - 1) * StepDb;
    }
}
