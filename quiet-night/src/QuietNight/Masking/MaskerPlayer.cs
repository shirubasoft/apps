using Android.Media;
using QuietNight.Core.Masking;

namespace QuietNight.Masking;

/// <summary>
/// Plays one looping noise layer per masking band, each on its own track. Boosts change the track volume, which the
/// system mixer applies on its next cycle, so they take effect without waiting for the half second of queued audio.
/// </summary>
public sealed class MaskerPlayer : IDisposable
{
    private const int BufferFrames = NoiseLoop.OutputSampleRate / 2;
    private const int ChunkFrames = NoiseLoop.OutputSampleRate / 20;
    private const float VolumeStepDb = 0.25f;

    private readonly float[][] loops;
    private readonly AudioTrack[] tracks;
    private readonly int[] positions;
    private readonly float[] appliedDb;
    private readonly Thread writer;
    private volatile bool running;
    private volatile bool muted;

    public MaskerPlayer(float[][] loops)
    {
        this.loops = loops;
        positions = new int[loops.Length];
        appliedDb = [.. Enumerable.Repeat(float.NaN, loops.Length)];
        var attributes = new AudioAttributes.Builder()
            .SetUsage(AudioUsageKind.Media)!
            .SetContentType(AudioContentType.Music)!
            .Build()!;
        var format = new AudioFormat.Builder()
            .SetEncoding(Encoding.PcmFloat)!
            .SetSampleRate(NoiseLoop.OutputSampleRate)!
            .SetChannelMask(ChannelOut.Mono)!
            .Build()!;
        tracks = [.. loops.Select(_ => new AudioTrack.Builder()
            .SetAudioAttributes(attributes)!
            .SetAudioFormat(format)!
            .SetBufferSizeInBytes(BufferFrames * sizeof(float))!
            .SetTransferMode(AudioTrackMode.Stream)!
            .Build()!)];
        writer = new Thread(Write) { IsBackground = true, Name = "Masker writer" };
    }

    /// <summary>Silences every layer while another app holds audio focus, such as during a call.</summary>
    public bool Muted
    {
        get => muted;
        set
        {
            muted = value;
            for (var b = 0; b < tracks.Length; b++)
                Apply(b, appliedDb[b], force: true);
        }
    }

    public void Start()
    {
        running = true;
        for (var b = 0; b < tracks.Length; b++)
        {
            tracks[b].SetVolume(0);
            Fill(b, BufferFrames);
            tracks[b].Play();
        }
        // Priming played silence; restore whatever levels were set before playback began.
        for (var b = 0; b < tracks.Length; b++)
            Apply(b, appliedDb[b], force: true);
        writer.Start();
    }

    /// <summary>
    /// Whether every layer is actually being played through earbuds or headphones. Null until Android has routed
    /// them; false as soon as any layer reaches another output such as the phone speaker.
    /// </summary>
    public bool? RoutedToEars()
    {
        var known = true;
        foreach (var track in tracks)
        {
            var device = track.RoutedDevice;
            if (device is null) known = false;
            else if (!Audio.AudioRoutes.IsEar(device.Type)) return false;
        }
        return known ? true : null;
    }

    /// <summary>Sets each layer's level in dB relative to full scale.</summary>
    public void SetLevels(ReadOnlySpan<float> levelDb)
    {
        for (var b = 0; b < tracks.Length; b++)
            if (float.IsNaN(appliedDb[b]) || MathF.Abs(levelDb[b] - appliedDb[b]) >= VolumeStepDb)
                Apply(b, levelDb[b], force: false);
    }

    public void Dispose()
    {
        running = false;
        if (writer.IsAlive) writer.Join(TimeSpan.FromSeconds(1));
        foreach (var track in tracks)
        {
            try
            {
                track.Pause();
                track.Flush();
            }
            catch (Java.Lang.IllegalStateException)
            {
            }
            track.Release();
            track.Dispose();
        }
    }

    private void Apply(int band, float levelDb, bool force)
    {
        if (float.IsNaN(levelDb) && !force) return;
        appliedDb[band] = levelDb;
        var volume = muted || float.IsNaN(levelDb) ? 0 : Math.Clamp(MathF.Pow(10, levelDb / 20), 0, 1);
        tracks[band].SetVolume(volume);
    }

    private void Write()
    {
        while (running)
        {
            for (var b = 0; b < tracks.Length && running; b++)
                Fill(b, ChunkFrames);
            Thread.Sleep(20);
        }
    }

    private void Fill(int band, int frames)
    {
        var loop = loops[band];
        while (frames > 0)
        {
            var count = Math.Min(frames, loop.Length - positions[band]);
            var written = tracks[band].Write(loop, positions[band], count, WriteMode.NonBlocking);
            if (written <= 0) return;
            positions[band] = (positions[band] + written) % loop.Length;
            frames -= written;
            if (written < count) return;
        }
    }
}
