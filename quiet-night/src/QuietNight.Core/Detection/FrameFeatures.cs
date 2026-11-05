using QuietNight.Core.Analysis;

namespace QuietNight.Core.Detection;

/// <summary>
/// The fast detector's view of one hop: how far each mel band rises above the room, how quickly it rose over the
/// last 40 ms, and the spectral shape independent of loudness.
/// </summary>
public sealed class FrameFeatures
{
    public const int Count = 3 * ListeningFormat.MelBands + 2;
    private const int DeltaHops = 4;
    private const float Scale = 20;

    private readonly float[][] history = [.. Enumerable.Range(0, DeltaHops + 1).Select(_ => new float[ListeningFormat.MelBands])];
    private int position;

    public void Compute(ReadOnlySpan<float> melDb, ReadOnlySpan<float> backgroundDb, Span<float> features)
    {
        const int bands = ListeningFormat.MelBands;
        var current = history[position];
        var past = history[(position + 1) % history.Length];
        var mean = 0f;
        for (var m = 0; m < bands; m++)
            mean += melDb[m];
        mean /= bands;
        var maxRise = float.MinValue;
        var meanRise = 0f;
        for (var m = 0; m < bands; m++)
        {
            var rise = Math.Clamp((melDb[m] - backgroundDb[m]) / Scale, -0.5f, 2f);
            current[m] = rise;
            features[m] = rise;
            features[bands + m] = rise - past[m];
            features[2 * bands + m] = Math.Clamp((melDb[m] - mean) / Scale, -3f, 3f);
            maxRise = MathF.Max(maxRise, rise);
            meanRise += rise / bands;
        }
        features[3 * bands] = maxRise;
        features[3 * bands + 1] = meanRise;
        position = (position + 1) % history.Length;
    }
}
