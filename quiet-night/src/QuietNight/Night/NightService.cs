using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using QuietNight.Training;

namespace QuietNight.Night;

/// <summary>
/// Keeps the night running with the screen off: a foreground service that may use the microphone and play media, with
/// a partial wake lock. It stops itself when the earbuds disconnect so the masker never plays from the speaker.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMicrophone | ForegroundService.TypeMediaPlayback)]
public sealed class NightService : Service
{
    public const string StopAction = "dev.danielreis.quietnight.STOP";
    private const string Channel = "night";
    private const int NotificationId = 1;

    private PowerManager.WakeLock? wakeLock;
    private NoisyReceiver? noisy;
    private AudioFocusRequestClass? focus;
    private NightEngine? engine;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == StopAction)
        {
            StopNight(null);
            return StartCommandResult.NotSticky;
        }
        if (engine is not null) return StartCommandResult.NotSticky;
        CreateChannel();
        StartForeground(NotificationId, Notification("Listening for cats, motorcycles and cars"),
            ForegroundService.TypeMicrophone | ForegroundService.TypeMediaPlayback);
        _ = StartEngineAsync();
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        TearDown();
        base.OnDestroy();
    }

    private async Task StartEngineAsync()
    {
        try
        {
            var settings = NightSettings.Load();
            var heads = await ModelStore.HeadsAsync();
            var yamnet = await ModelStore.YamnetAsync();
            var layers = await Task.Run(ModelStore.Layers);
            var created = new NightEngine(this, settings, heads, yamnet, layers);
            created.Published += OnPublished;
            created.Start();
            engine = created;
            NightSession.Engine = created;
            Hold();
            NightSession.Report(NightStatus.Idle with { Running = true, Since = DateTimeOffset.Now });
        }
        catch (Exception e) when (e is InvalidOperationException or Java.Lang.Exception or IOException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException)
        {
            StopNight(e.Message);
        }
    }

    private void OnPublished(NightStatus status)
    {
        NightSession.Report(status);
        if (status.Problem is { } problem)
            MainThread.BeginInvokeOnMainThread(() => StopNight(problem));
    }

    private void Hold()
    {
        var power = (PowerManager?)GetSystemService(PowerService);
        wakeLock = power?.NewWakeLock(WakeLockFlags.Partial, "QuietNight:night");
        wakeLock?.Acquire();
        noisy = new NoisyReceiver(() => StopNight("Your earbuds disconnected, so the masker stopped instead of playing from the speaker."));
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            RegisterReceiver(noisy, new IntentFilter(AudioManager.ActionAudioBecomingNoisy), ReceiverFlags.NotExported);
        else
            RegisterReceiver(noisy, new IntentFilter(AudioManager.ActionAudioBecomingNoisy));
        var audio = (AudioManager?)GetSystemService(AudioService);
        var attributes = new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media)!.SetContentType(AudioContentType.Music)!.Build()!;
        focus = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
            .SetAudioAttributes(attributes)!
            .SetWillPauseWhenDucked(true)!
            .SetOnAudioFocusChangeListener(new FocusListener(this))!
            .Build();
        if (focus is not null) audio?.RequestAudioFocus(focus);
    }

    private void StopNight(string? problem)
    {
        TearDown();
        NightSession.Report(NightStatus.Idle with { Problem = problem });
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
    }

    private void TearDown()
    {
        var running = engine;
        engine = null;
        if (running is not null)
        {
            running.Published -= OnPublished;
            running.Dispose();
        }
        if (noisy is not null)
        {
            UnregisterReceiver(noisy);
            noisy = null;
        }
        if (focus is not null)
        {
            ((AudioManager?)GetSystemService(AudioService))?.AbandonAudioFocusRequest(focus);
            focus = null;
        }
        if (wakeLock is { IsHeld: true }) wakeLock.Release();
        wakeLock = null;
        NightSession.Engine = null;
    }

    private void CreateChannel()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(Channel, "Night session", NotificationImportance.Low)
        {
            Description = "Shown while Quiet Night listens and masks noises.",
        });
    }

    private Notification Notification(string text)
    {
        var open = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.SingleTop),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var stop = PendingIntent.GetService(this, 1, new Intent(this, typeof(NightService)).SetAction(StopAction),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        return new Notification.Builder(this, Channel)
            .SetContentTitle("Quiet Night")!
            .SetContentText(text)!
            .SetSmallIcon(Resource.Drawable.ic_stat_night)!
            .SetOngoing(true)!
            .SetContentIntent(open)!
            .AddAction(new Notification.Action.Builder(null, "Stop", stop).Build())!
            .Build()!;
    }

    private sealed class NoisyReceiver(Action onNoisy) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy) onNoisy();
        }
    }

    private sealed class FocusListener(NightService service) : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            if (service.engine is { } running)
                running.Muted = focusChange is AudioFocus.Loss or AudioFocus.LossTransient or AudioFocus.LossTransientCanDuck;
        }
    }
}
