using Android.Content;
using QuietNight.Core.Detection;
using QuietNight.Core.Recordings;
using QuietNight.Training;

namespace QuietNight.Night;

/// <summary>Connects the screen to the night service, which may outlive any page.</summary>
public static class NightSession
{
    private static NightStatus status = NightStatus.Idle;

    public static NightStatus Status => Volatile.Read(ref status);

    internal static NightEngine? Engine { get; set; }

    public static void Start(Context context) =>
        context.StartForegroundService(new Intent(context, typeof(NightService)));

    public static void Stop(Context context) =>
        context.StartService(new Intent(context, typeof(NightService)).SetAction(NightService.StopAction));

    /// <summary>Saves what the microphone heard over the last 20 s as a labelled recording.</summary>
    public static Recording? SaveRecent(NoiseClass label)
    {
        var samples = Engine?.Recent();
        return samples is { Length: > 0 } ? ModelStore.Recordings.Save(samples, label, DateTimeOffset.Now) : null;
    }

    internal static void Report(NightStatus next) => Volatile.Write(ref status, next);
}
