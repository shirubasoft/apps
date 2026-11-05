using Android.Media;
using QuietNight.Core.Analysis;

namespace QuietNight.Training;

/// <summary>Plays a recording back once so the user can check what they captured.</summary>
public sealed class ClipPlayer : IDisposable
{
    private AudioTrack? track;
    private CancellationTokenSource? playing;

    public bool IsPlaying => playing is { IsCancellationRequested: false };

    public async Task PlayAsync(float[] samples)
    {
        Stop();
        var cancel = new CancellationTokenSource();
        playing = cancel;
        var current = new AudioTrack.Builder()
            .SetAudioAttributes(new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media)!.SetContentType(AudioContentType.Music)!.Build()!)!
            .SetAudioFormat(new AudioFormat.Builder().SetEncoding(Encoding.PcmFloat)!.SetSampleRate(ListeningFormat.SampleRate)!.SetChannelMask(ChannelOut.Mono)!.Build()!)!
            .SetBufferSizeInBytes(ListeningFormat.SampleRate * sizeof(float) / 2)!
            .SetTransferMode(AudioTrackMode.Stream)!
            .Build()!;
        track = current;
        current.Play();
        await Task.Run(() =>
        {
            for (var start = 0; start < samples.Length && !cancel.IsCancellationRequested; start += ListeningFormat.HopSamples * 10)
                current.Write(samples, start, Math.Min(ListeningFormat.HopSamples * 10, samples.Length - start), WriteMode.Blocking);
        });
        if (!cancel.IsCancellationRequested)
            await Task.Delay(500);
        if (ReferenceEquals(track, current)) Stop();
    }

    public void Stop()
    {
        playing?.Cancel();
        playing = null;
        var current = track;
        track = null;
        if (current is null) return;
        try
        {
            current.Stop();
        }
        catch (Java.Lang.IllegalStateException)
        {
        }
        current.Release();
        current.Dispose();
    }

    public void Dispose() => Stop();
}
