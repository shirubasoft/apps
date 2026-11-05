using QuietNight.Core.Analysis;

namespace QuietNight.Core.Masking;

/// <summary>
/// Builds seamless loops of band-limited noise. Each loop is the inverse FFT of a random-phase spectrum, so its end
/// joins its start without a click and the octave layers add up to an even noise.
/// </summary>
public static class NoiseLoop
{
    public const int OutputSampleRate = 48_000;
    public const int LengthLog2 = 19;

    /// <summary>A loop of about 11 s whose RMS level is <paramref name="rmsDb"/> relative to full scale.</summary>
    public static float[] Create(double lowHertz, double highHertz, int seed, float rmsDb = -20, int lengthLog2 = LengthLog2)
    {
        var length = 1 << lengthLog2;
        var real = new float[length];
        var imaginary = new float[length];
        var random = new Random(seed);
        var hertzPerBin = (double)OutputSampleRate / length;
        var first = Math.Max(1, (int)Math.Ceiling(lowHertz / hertzPerBin));
        var last = Math.Min(length / 2 - 1, (int)Math.Floor(highHertz / hertzPerBin));
        for (var k = first; k <= last; k++)
        {
            var phase = random.NextDouble() * 2 * Math.PI;
            real[k] = (float)Math.Cos(phase);
            imaginary[k] = (float)Math.Sin(phase);
            real[length - k] = real[k];
            imaginary[length - k] = -imaginary[k];
        }
        new Fft(length).Inverse(real, imaginary);
        var energy = 0.0;
        foreach (var sample in real)
            energy += sample * sample;
        var rms = Math.Sqrt(energy / length);
        var gain = (float)(Math.Pow(10, rmsDb / 20) / Math.Max(rms, 1e-12));
        for (var i = 0; i < length; i++)
            real[i] *= gain;
        return real;
    }

    /// <summary>One loop per masking band.</summary>
    public static float[][] CreateLayers(int seed = 7, float rmsDb = -20, int lengthLog2 = LengthLog2) =>
        [.. MaskerBands.LayerHertz.Select((band, i) => Create(band.Low, band.High, seed + i, rmsDb, lengthLog2))];
}
