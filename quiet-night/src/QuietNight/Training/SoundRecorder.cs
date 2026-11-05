using Android.Media;
using QuietNight.Audio;
using QuietNight.Core.Analysis;

namespace QuietNight.Training;

/// <summary>
/// Listens with the same microphone setup the night uses, keeping only the latest 30 s, so the user can start it and
/// wait for a noise that comes when it pleases. It stops by itself after ten minutes.
/// </summary>
public sealed class SoundRecorder : IDisposable
{
    public static readonly TimeSpan KeptLength = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(10);

    private readonly AudioRecord record;
    private readonly float[] ring = new float[(int)(KeptLength.TotalSeconds * ListeningFormat.SampleRate)];
    private readonly Thread thread;
    private volatile bool running;
    private long count;
    private volatile float levelDb = float.NegativeInfinity;

    public SoundRecorder()
    {
        record = Microphone.Open(Platform.AppContext);
        thread = new Thread(Read) { IsBackground = true, Name = "Recorder" };
    }

    /// <summary>How long it has been listening.</summary>
    public TimeSpan Elapsed => TimeSpan.FromSeconds((double)Interlocked.Read(ref count) / ListeningFormat.SampleRate);

    /// <summary>The level of the latest 10 ms, in dB relative to full scale.</summary>
    public float LevelDb => levelDb;

    /// <summary>True once it has waited as long as it will.</summary>
    public bool Done => Elapsed >= MaxWait || (!running && Interlocked.Read(ref count) > 0);

    public void Start()
    {
        running = true;
        record.StartRecording();
        thread.Start();
    }

    /// <summary>Stops and returns the latest 30 s, oldest first.</summary>
    public float[] Stop()
    {
        running = false;
        record.Stop();
        thread.Join(TimeSpan.FromSeconds(1));
        var total = Interlocked.Read(ref count);
        if (total <= ring.Length) return ring[..(int)total];
        var start = (int)(total % ring.Length);
        return [.. ring[start..], .. ring[..start]];
    }

    public void Dispose()
    {
        running = false;
        record.Release();
        record.Dispose();
    }

    private void Read()
    {
        var hop = new float[ListeningFormat.HopSamples];
        while (running && Elapsed < MaxWait)
        {
            var read = record.Read(hop, 0, hop.Length, 0);
            if (read <= 0) break;
            levelDb = Loudness.RmsDb(hop.AsSpan(0, read));
            var at = (int)(Interlocked.Read(ref count) % ring.Length);
            var first = Math.Min(read, ring.Length - at);
            hop.AsSpan(0, first).CopyTo(ring.AsSpan(at));
            hop.AsSpan(first, read - first).CopyTo(ring);
            Interlocked.Add(ref count, read);
        }
        running = false;
    }
}
