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
        return outputs.Any(d => IsEar(d.Type));
    }

    public static bool IsEar(AudioDeviceType type) => EarTypes.Contains(type);

    /// <summary>The name of the earbuds or headphones the night will play in, such as "Pixel Buds Pro", or null.</summary>
    public static string? EarbudsName(Context context)
    {
        var audio = (AudioManager?)context.GetSystemService(Context.AudioService);
        var ear = (audio?.GetDevices(GetDevicesTargets.Outputs) ?? []).FirstOrDefault(d => IsEar(d.Type));
        var name = ear?.ProductName?.ToString();
        // Wired headphones often report the phone's own model as their name, which says nothing useful.
        return string.IsNullOrWhiteSpace(name) || name == Android.OS.Build.Model ? null : name;
    }
}
