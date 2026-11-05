using QuietNight.Core.Analysis;

namespace QuietNight.Core.Training;

/// <summary>A synthetic quiet bedroom: soft pink noise with a little microphone hiss, used when no room recording exists.</summary>
public static class RoomBed
{
    /// <summary>
    /// The self-noise of a typical phone microphone: a MEMS capsule of -26 dBFS sensitivity at 94 dB SPL and 65 dB
    /// signal-to-noise ratio hisses at about 29 dB SPL, or -90 dBFS. Every real capture contains it.
    /// </summary>
    public const float MicrophoneNoiseDb = -90;

    /// <summary>Adds deterministic microphone self-noise to <paramref name="samples"/>.</summary>
    public static void AddMicrophoneNoise(Span<float> samples, int seed)
    {
        var random = new Random(seed);
        var amplitude = MathF.Pow(10, MicrophoneNoiseDb / 20) * MathF.Sqrt(3);
        foreach (ref var sample in samples)
            sample += amplitude * (float)(random.NextDouble() * 2 - 1);
    }

    /// <summary>
    /// Turns a room recording into a loop: drops the first and last half second, where recordings fade, and crossfades
    /// the end into the start so the room never drops out when it repeats.
    /// </summary>
    public static float[] Loop(ReadOnlySpan<float> recording)
    {
        var trim = ListeningFormat.SampleRate / 2;
        var fade = ListeningFormat.SampleRate;
        if (recording.Length < 2 * trim + 2 * fade) return recording.ToArray();
        var body = recording[trim..^trim];
        var loop = body[..^fade].ToArray();
        for (var i = 0; i < fade; i++)
        {
            var w = (float)i / fade;
            loop[i] = loop[i] * w + body[body.Length - fade + i] * (1 - w);
        }
        return loop;
    }

    public static float[] Create(int seed = 3, float levelDb = -50, int lengthLog2 = 17)
    {
        var length = 1 << lengthLog2;
        var real = new float[length];
        var imaginary = new float[length];
        var random = new Random(seed);
        for (var k = 1; k < length / 2; k++)
        {
            var hertz = (double)k * ListeningFormat.SampleRate / length;
            var amplitude = (float)(1 / Math.Sqrt(Math.Max(hertz, 20)) + 0.004);
            var phase = random.NextDouble() * 2 * Math.PI;
            real[k] = amplitude * (float)Math.Cos(phase);
            imaginary[k] = amplitude * (float)Math.Sin(phase);
            real[length - k] = real[k];
            imaginary[length - k] = -imaginary[k];
        }
        new Fft(length).Inverse(real, imaginary);
        var gain = MathF.Pow(10, (levelDb - Loudness.RmsDb(real)) / 20);
        for (var i = 0; i < length; i++)
            real[i] *= gain;
        return real;
    }
}
