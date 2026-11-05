using Android.Content;
using Android.Media;
using QuietNight.Core.Analysis;

namespace QuietNight.Audio;

/// <summary>
/// Opens the phone microphone at 16 kHz with the least processing the device offers. Automatic gain and noise
/// suppression would flatten exactly the sounds the app listens for.
/// </summary>
public static class Microphone
{
    private const int BufferSeconds = 1;

    public static AudioRecord Open(Context context)
    {
        var audio = (AudioManager?)context.GetSystemService(Context.AudioService);
        var source = audio?.GetProperty(AudioManager.PropertySupportAudioSourceUnprocessed) == "true"
            ? AudioSource.Unprocessed
            : AudioSource.VoiceRecognition;
        var minimum = AudioRecord.GetMinBufferSize(ListeningFormat.SampleRate, ChannelIn.Mono, Encoding.PcmFloat);
        var size = Math.Max(minimum, ListeningFormat.SampleRate * sizeof(float) * BufferSeconds);
        var record = new AudioRecord(source, ListeningFormat.SampleRate, ChannelIn.Mono, Encoding.PcmFloat, size);
        if (record.State != State.Initialized)
        {
            record.Release();
            throw new InvalidOperationException("The microphone could not be opened. Another app may be using it.");
        }
        return record;
    }
}
