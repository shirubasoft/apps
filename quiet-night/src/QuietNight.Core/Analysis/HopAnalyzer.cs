namespace QuietNight.Core.Analysis;

/// <summary>
/// Turns each 10 ms microphone hop into smoothed mel and masker-band levels in dB relative to full scale.
/// Levels are averaged in the power domain over about 40 ms, close to the ear's shortest integration time.
/// </summary>
public sealed class HopAnalyzer
{
    public const float SmoothingSeconds = 0.04f;
    private const float FloorPower = 1e-12f;

    private static readonly float[] Window = CreateWindow();
    private static readonly float PowerScale = CreatePowerScale();
    private static readonly MelBank Mel = new();

    private readonly float[] history = new float[ListeningFormat.WindowSamples];
    private readonly Fft fft = new(ListeningFormat.FftSize);
    private readonly float[] real = new float[ListeningFormat.FftSize];
    private readonly float[] imaginary = new float[ListeningFormat.FftSize];
    private readonly float[] power = new float[ListeningFormat.Bins];
    private readonly float[] melPower = new float[ListeningFormat.MelBands];
    private readonly float[] bandPower = new float[MaskerBands.Count];
    private readonly float[] melSmooth = new float[ListeningFormat.MelBands];
    private readonly float[] bandSmooth = new float[MaskerBands.Count];
    private readonly float[] melDb = new float[ListeningFormat.MelBands];
    private readonly float[] bandDb = new float[MaskerBands.Count];
    private readonly float alpha = 1 - MathF.Exp(-ListeningFormat.HopSeconds / SmoothingSeconds);
    private bool primed;

    public ReadOnlySpan<float> MelDb => melDb;

    public ReadOnlySpan<float> BandDb => bandDb;

    /// <summary>Smoothed band power, linear, for callers that combine signals.</summary>
    public ReadOnlySpan<float> BandPower => bandSmooth;

    public void Process(ReadOnlySpan<float> hop)
    {
        if (hop.Length != ListeningFormat.HopSamples)
            throw new ArgumentException($"A hop holds {ListeningFormat.HopSamples} samples.", nameof(hop));
        const int keep = ListeningFormat.WindowSamples - ListeningFormat.HopSamples;
        history.AsSpan(ListeningFormat.HopSamples).CopyTo(history);
        hop.CopyTo(history.AsSpan(keep));
        for (var i = 0; i < ListeningFormat.WindowSamples; i++)
            real[i] = history[i] * Window[i];
        real.AsSpan(ListeningFormat.WindowSamples).Clear();
        imaginary.AsSpan().Clear();
        fft.Forward(real, imaginary);
        for (var k = 0; k < ListeningFormat.Bins; k++)
            power[k] = (real[k] * real[k] + imaginary[k] * imaginary[k]) * PowerScale;
        Mel.Apply(power, melPower);
        for (var b = 0; b < MaskerBands.Count; b++)
        {
            var sum = 0f;
            for (var k = MaskerBands.First(b); k < MaskerBands.End(b); k++)
                sum += power[k];
            bandPower[b] = sum;
        }
        Smooth(melPower, melSmooth, melDb);
        Smooth(bandPower, bandSmooth, bandDb);
        primed = true;
    }

    public static float ToDb(float power) => 10 * MathF.Log10(power + FloorPower);

    private void Smooth(float[] raw, float[] smooth, float[] db)
    {
        for (var i = 0; i < raw.Length; i++)
        {
            smooth[i] = primed ? smooth[i] + alpha * (raw[i] - smooth[i]) : raw[i];
            db[i] = ToDb(smooth[i]);
        }
    }

    private static float[] CreateWindow()
    {
        var window = new float[ListeningFormat.WindowSamples];
        for (var i = 0; i < window.Length; i++)
            window[i] = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * i / (window.Length - 1));
        return window;
    }

    /// <summary>One-sided spectral power scaled so the bins of a band add up to the signal's mean square there.</summary>
    private static float CreatePowerScale() => 2f / (ListeningFormat.FftSize * Window.Sum(w => w * w));
}
