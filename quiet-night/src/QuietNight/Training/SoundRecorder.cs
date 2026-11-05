using Android.Media;
using QuietNight.Audio;
using QuietNight.Core.Analysis;

namespace QuietNight.Training;

/// <summary>Records a labelled sound from the same microphone setup the night uses, for at most 30 s.</summary>
public sealed class SoundRecorder : IDisposable
{
    public static readonly TimeSpan MaxLength = TimeSpan.FromSeconds(30);

    private readonly AudioRecord record;
    private readonly float[] samples = new float[(int)(MaxLength.TotalSeconds * ListeningFormat.SampleRate)];
    private readonly Thread thread;
    private volatile bool running;
    private volatile int count;
    private volatile float levelDb = float.NegativeInfinity;

    public SoundRecorder()
    {
        record = Microphone.Open(Platform.AppContext);
        thread = new Thread(Read) { IsBackground = true, Name = "Recorder" };
    }

    public TimeSpan Elapsed => TimeSpan.FromSeconds((double)count / ListeningFormat.SampleRate);

    /// <summary>The level of the latest 10 ms, in dB relative to full scale.</summary>
    public float LevelDb => levelDb;

    public bool Full => count >= samples.Length;

    public void Start()
    {
        running = true;
        record.StartRecording();
        thread.Start();
    }

    public float[] Stop()
    {
        running = false;
        record.Stop();
        thread.Join(TimeSpan.FromSeconds(1));
        return samples[..count];
    }

    public void Dispose()
    {
        running = false;
        record.Release();
        record.Dispose();
    }

    private void Read()
    {
        while (running && count < samples.Length)
        {
            var wanted = Math.Min(ListeningFormat.HopSamples, samples.Length - count);
            var read = record.Read(samples, count, wanted, 0);
            if (read <= 0) break;
            levelDb = Loudness.RmsDb(samples.AsSpan(count, read));
            count += read;
        }
        running = false;
    }
}
