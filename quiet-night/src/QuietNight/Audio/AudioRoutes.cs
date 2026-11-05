using Android.Content;
using Android.Media;

namespace QuietNight.Audio;

/// <summary>
/// Masking only makes sense in the ears. Through the phone speaker the microphone would hear the masker and chase it.
/// </summary>
public static class AudioRoutes
{
    private static readonly AudioDeviceType[] EarTypes =
    [
        AudioDeviceType.BluetoothA2dp, AudioDeviceType.WiredHeadphones, AudioDeviceType.WiredHeadset,
        AudioDeviceType.UsbHeadset, AudioDeviceType.HearingAid,
        .. OperatingSystem.IsAndroidVersionAtLeast(31) ? [AudioDeviceType.BleHeadset] : Array.Empty<AudioDeviceType>(),
    ];

    public static bool HeadphonesConnected(Context context)
    {
        var audio = (AudioManager?)context.GetSystemService(Context.AudioService);
        var outputs = audio?.GetDevices(GetDevicesTargets.Outputs) ?? [];
        return outputs.Any(d => EarTypes.Contains(d.Type));
    }
}
