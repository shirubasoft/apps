using System.Diagnostics;
using QuietNight.Core.Analysis;
using QuietNight.Core.Training;

namespace QuietNight.Trainer;

/// <summary>Decodes and trims downloaded reference clips with ffmpeg.</summary>
internal static class ReferenceAudio
{
    public static readonly TimeSpan EventLength = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan TrainingLength = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan BackgroundLength = TimeSpan.FromSeconds(30);

    public static string Mp3(string directory, ReferenceSound sound) => Path.Combine(directory, $"{sound.Id}.mp3");

    public static float[] Decode(string path)
    {
        var start = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-v", "error", "-nostdin", "-i", path, "-ac", "1", "-ar", $"{ListeningFormat.SampleRate}", "-f", "f32le", "-" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("ffmpeg did not start.");
        using var buffer = new MemoryStream();
        var errors = process.StandardError.ReadToEndAsync();
        process.StandardOutput.BaseStream.CopyTo(buffer);
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg could not decode {path}: {errors.Result}");
        var bytes = buffer.ToArray();
        var samples = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, samples, 0, samples.Length * 4);
        return samples;
    }

    /// <summary>The loudest stretch of at most <paramref name="length"/>, so pass-bys keep their peak.</summary>
    public static float[] Loudest(float[] samples, TimeSpan length)
    {
        var wanted = (int)(length.TotalSeconds * ListeningFormat.SampleRate);
        if (samples.Length <= wanted) return samples;
        var step = ListeningFormat.SampleRate / 10;
        var best = 0;
        var bestEnergy = double.MinValue;
        for (var start = 0; start + wanted <= samples.Length; start += step)
        {
            var energy = 0.0;
            for (var i = start; i < start + wanted; i += 4)
                energy += samples[i] * samples[i];
            if (energy > bestEnergy)
            {
                bestEnergy = energy;
                best = start;
            }
        }
        return samples[best..(best + wanted)];
    }

    /// <summary>Encodes 16 kHz mono samples as Ogg Opus at 32 kbit/s.</summary>
    public static void EncodeOpus(float[] samples, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var start = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[] { "-v", "error", "-y", "-f", "f32le", "-ar", $"{ListeningFormat.SampleRate}", "-ac", "1", "-i", "-", "-c:a", "libopus", "-b:a", "32k", "-application", "audio", "-map_metadata", "-1", path })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("ffmpeg did not start.");
        var errors = process.StandardError.ReadToEndAsync();
        var bytes = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        process.StandardInput.BaseStream.Write(bytes);
        process.StandardInput.Close();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg could not encode {path}: {errors.Result}");
    }
}
