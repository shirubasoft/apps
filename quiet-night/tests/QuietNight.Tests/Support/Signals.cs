using QuietNight.Core.Analysis;

namespace QuietNight.Tests.Support;

/// <summary>Deterministic test signals at the listening rate.</summary>
public static class Signals
{
    public static float[] Sine(double hertz, float amplitude, TimeSpan duration) =>
        [.. Enumerable.Range(0, Samples(duration)).Select(i => amplitude * (float)Math.Sin(2 * Math.PI * hertz * i / ListeningFormat.SampleRate))];

    public static float[] Noise(float amplitude, TimeSpan duration, int seed)
    {
        var random = new Random(seed);
        return [.. Enumerable.Range(0, Samples(duration)).Select(_ => amplitude * (float)(random.NextDouble() * 2 - 1))];
    }

    public static float[] Concat(params float[][] parts) => [.. parts.SelectMany(p => p)];

    public static float[] Add(float[] a, float[] b) => [.. a.Zip(b, (x, y) => x + y)];

    public static int Samples(TimeSpan duration) => (int)(duration.TotalSeconds * ListeningFormat.SampleRate);

    public static IEnumerable<ReadOnlyMemory<float>> Hops(float[] samples)
    {
        for (var start = 0; start + ListeningFormat.HopSamples <= samples.Length; start += ListeningFormat.HopSamples)
            yield return samples.AsMemory(start, ListeningFormat.HopSamples);
    }
}
