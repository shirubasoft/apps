namespace QuietNight.Core.Analysis;

/// <summary>Levels of whole clips, used to mix sounds at a chosen loudness above a room.</summary>
public static class Loudness
{
    private const int BlockSamples = ListeningFormat.SampleRate / 10;
    private const float ActiveRangeDb = 20;

    /// <summary>RMS level in dB relative to full scale.</summary>
    public static float RmsDb(ReadOnlySpan<float> samples)
    {
        var energy = 0.0;
        foreach (var sample in samples)
            energy += sample * sample;
        return HopAnalyzer.ToDb((float)(energy / Math.Max(1, samples.Length)));
    }

    /// <summary>
    /// RMS over the 100 ms blocks within 20 dB of the loudest one, so the pauses between meows do not make a cat
    /// count as quieter than it sounds.
    /// </summary>
    public static float ActiveRmsDb(ReadOnlySpan<float> samples)
    {
        var blocks = Math.Max(1, samples.Length / BlockSamples);
        var energies = new double[blocks];
        for (var b = 0; b < blocks; b++)
        {
            var block = samples.Slice(b * BlockSamples, Math.Min(BlockSamples, samples.Length - b * BlockSamples));
            foreach (var sample in block)
                energies[b] += sample * sample;
            energies[b] /= Math.Max(1, block.Length);
        }
        var threshold = energies.Max() * Math.Pow(10, -ActiveRangeDb / 10);
        var active = energies.Where(e => e >= threshold).ToArray();
        return HopAnalyzer.ToDb((float)active.Average());
    }

    /// <summary>Scales <paramref name="samples"/> in place so its active level is <paramref name="targetDb"/>.</summary>
    public static void ScaleTo(Span<float> samples, float targetDb)
    {
        var gain = MathF.Pow(10, (targetDb - ActiveRmsDb(samples)) / 20);
        foreach (ref var sample in samples)
            sample *= gain;
    }
}
