using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using QuietNight.Core.Detection;
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
    public const string QuieterAction = "dev.danielreis.quietnight.QUIETER";
    public const string SaveAction = "dev.danielreis.quietnight.SAVE";
    private const string Channel = "night-session";
    private const string OldChannel = "night";
    private const int NotificationId = 1;
    private const int StoppedNotificationId = 2;

    private PowerManager.WakeLock? wakeLock;
    private NoisyReceiver? noisy;
    private AudioFocusRequestClass? focus;
    private NightEngine? engine;
    private bool starting;
    private bool focusGranted;
    private int generation;
    private string? notified;
    private DateTimeOffset savedUntil;
    private string listeningFor = "cats, motorcycles and cars";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == SaveAction)
        {
            // From the lock screen, the last 20 seconds are kept under the sound just masked, or as unsorted otherwise;
            // the Teach tab can rename it.
            if (NightSession.CaptureRecent() is { } samples)
            {
                var status = NightSession.Status;
                var recent = status.Masked.Count > 0 && DateTimeOffset.Now - status.Masked[^1].At < NightEngine.RecentLength
                    ? status.Masked[^1].Noise
                    : NoiseClass.Other;
                // Without a sound just masked there is no guess, so the clip waits, on the phone, for the user to say what it was.
                try
                {
                    if (recent == NoiseClass.Other) NightSession.KeepUnsorted(samples);
                    else NightSession.Save(samples, recent);
                    notified = recent == NoiseClass.Other
                        ? "Saved the last 20 seconds. Open Quiet Night to say what it was."
                        : $"Saved the last 20 seconds as {NightSettings.Describe(recent)} sound, its best guess. Rename it on the Teach tab if that is wrong.";
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    notified = "The last 20 seconds could not be saved: the phone's storage may be full.";
                }
                ((NotificationManager?)GetSystemService(NotificationService))?.Notify(NotificationId, Notification(notified));
                // The confirmation stays for half a minute before the night's own line returns.
                savedUntil = DateTimeOffset.Now.AddSeconds(30);
            }
            return StartCommandResult.NotSticky;
        }
        if (intent?.Action == QuieterAction)
        {
            // From the lock screen, one step quieter without unlocking the phone.
            var current = NightSettings.Load();
            var next = current with { Volume = Math.Clamp(current.Volume - 0.1f, 0, 1) };
            next.Save();
            engine?.SetVolume(next.Volume);
            // The new volume shows on the lock screen at once.
            savedUntil = default;
            UpdateNotification(NightSession.Status);
            return StartCommandResult.NotSticky;
        }
        if (intent?.Action == StopAction)
        {
            StopNight(null);
            return StartCommandResult.NotSticky;
        }
        if (engine is not null || starting) return StartCommandResult.NotSticky;
        starting = true;
        CreateChannel();
        ((NotificationManager?)GetSystemService(NotificationService))?.Cancel(StoppedNotificationId);
        notified = null;
        StartForeground(NotificationId, Notification("Starting the night"),
            ForegroundService.TypeMicrophone | ForegroundService.TypeMediaPlayback);
        // Watch for earbuds disconnecting and take audio focus before a single sample plays.
        Hold();
        _ = StartEngineAsync(++generation);
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        // A start-up still loading models must not bring the night back once the service is gone.
        generation++;
        starting = false;
        TearDown();
        base.OnDestroy();
    }

    /// <summary>
    /// Loads the models and starts the engine. A stop while loading bumps <see cref="generation"/>, so a late
    /// continuation disposes what it built instead of bringing a stopped night back.
    /// </summary>
    private async Task StartEngineAsync(int started)
    {
        NightEngine? created = null;
        try
        {
            var settings = NightSettings.Load();
            listeningFor = settings.Masked();
            NightSession.ListeningFor = listeningFor;
            NightSession.StopsAt = NightSettings.NextStop(DateTime.Now);
            var heads = await ModelStore.HeadsAsync();
            var yamnet = await ModelStore.YamnetAsync();
            var layers = await Task.Run(ModelStore.Layers);
            if (started != generation) return;
            created = new NightEngine(this, settings, heads, yamnet, layers) { FocusGranted = focusGranted };
            created.Published += OnPublished;
            created.Start();
            engine = created;
            created = null;
            starting = false;
            NightSession.Engine = engine;
            NightSession.Report(NightStatus.Idle with { Running = true, Since = DateTimeOffset.Now });
            // A morning stop time ends the night by itself. It is checked every half minute, so a time changed during
            // the night counts; a later stop or start makes this watch stale.
            _ = WatchStopTimeAsync(started);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException)
        {
            // The reason goes to the page and the notification, so it says what to do in plain words.
            if (started == generation)
                StopNight("The night could not start: the sound model did not load. Close the app fully and open it again. If it keeps happening, reinstall it; that removes your recordings, so it is a last resort.", e.Message);
        }
        catch (Exception e) when (e is InvalidOperationException or Java.Lang.Exception)
        {
            if (started == generation)
                StopNight("The night could not start: the microphone or the earbuds did not respond. Close any app that may be using the microphone, check the earbuds, then try again.", e.Message);
        }
        finally
        {
            if (created is not null)
            {
                created.Published -= OnPublished;
                created.Dispose();
            }
        }
    }

    private async Task WatchStopTimeAsync(int started)
    {
        while (started == generation)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            if (started == generation && engine is not null && NightSession.StopsAt is { } at && DateTime.Now >= at)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (started == generation && engine is not null) StopNight(null);
                });
        }
    }

    private void OnPublished(NightStatus status)
    {
        NightSession.Report(status);
        UpdateNotification(status);
        if (status.Problem is { } problem)
            MainThread.BeginInvokeOnMainThread(() => StopNight(problem));
    }

    /// <summary>The notification follows the night, so the lock screen says what the page would, with the volume.</summary>
    private void UpdateNotification(NightStatus status)
    {
        if (DateTimeOffset.Now < savedUntil) return;
        if (status is { Running: true, Since: { } since } && Volatile.Read(ref engine) is not null)
        {
            var at = since.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
            var rose = NightSummary.Rises(status.Masked) is { } rises
                ? $"{rises} since {at}, last at {status.Masked[^1].At.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}, for {NightSummary.Detected(status.Masked)}."
                : null;
            var text = status.Paused
                ? "Paused: the masking sound is off while another app plays sound. It comes back on its own."
                : status.LoudestBoostDb > 0 && status.Masking.IsTarget()
                    ? $"Covering {NightSettings.Describe(status.Masking)} sound now. " + (rose ?? $"Listening since {at}.")
                    : rose ?? $"Listening for {listeningFor} since {at}.";
            text += $" Volume {NightSettings.Load().Volume:P0}.";
            if (text != notified)
            {
                notified = text;
                ((NotificationManager?)GetSystemService(NotificationService))?.Notify(NotificationId, Notification(text));
            }
        }
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
        // Without focus the masker stays silent until it is granted; another app may be playing.
        focusGranted = focus is not null && audio?.RequestAudioFocus(focus) == AudioFocusRequest.Granted;
    }

    /// <param name="problem">Why the night stopped by itself, in plain words; null when the user stopped it.</param>
    /// <param name="details">Technical details for the curious, kept out of the main message.</param>
    private void StopNight(string? problem, string? details = null)
    {
        NightSession.ProblemDetails = details;
        generation++;
        starting = false;
        TearDown();
        NightSession.Report(NightStatus.Idle with { Problem = problem });
        StopForeground(StopForegroundFlags.Remove);
        // A night that stops by itself says so where the user will look in the morning.
        if (problem is not null)
            ((NotificationManager?)GetSystemService(NotificationService))?.Notify(StoppedNotificationId, new Notification.Builder(this, Channel)
                .SetContentTitle("Quiet Night stopped")!
                .SetContentText(problem)!
                .SetStyle(new Notification.BigTextStyle().BigText(problem))!
                .SetSmallIcon(Resource.Drawable.ic_stat_night)!
                .SetAutoCancel(true)!
                .SetContentIntent(OpenApp())!
                .Build()!);
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
        focusGranted = false;
        if (wakeLock is { IsHeld: true }) wakeLock.Release();
        wakeLock = null;
        NightSession.Engine = null;
    }

    private void CreateChannel()
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        // A default-importance channel keeps the Stop action visible on lock screens that fold low ones away; it is
        // silent and never vibrates, so it cannot wake anyone. Android keeps a channel's importance once made, so
        // the quieter channel of earlier versions is replaced.
        manager?.DeleteNotificationChannel(OldChannel);
        var channel = new NotificationChannel(Channel, "Night session", NotificationImportance.Default)
        {
            Description = "Shown while Quiet Night listens and masks noises.",
            LockscreenVisibility = NotificationVisibility.Public,
        };
        channel.SetSound(null, null);
        channel.EnableVibration(false);
        manager?.CreateNotificationChannel(channel);
    }

    private Notification Notification(string text)
    {
        var stop = PendingIntent.GetService(this, 1, new Intent(this, typeof(NightService)).SetAction(StopAction),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        return new Notification.Builder(this, Channel)
            .SetContentTitle("Quiet Night")!
            .SetContentText(text)!
            .SetStyle(new Notification.BigTextStyle().BigText(text))!
            .SetVisibility(NotificationVisibility.Public)!
            .SetSmallIcon(Resource.Drawable.ic_stat_night)!
            .SetOngoing(true)!
            .SetContentIntent(OpenApp())!
            // Android shows three actions at most: quieter for a rise that woke the user, keeping what they heard, and Stop.
            .AddAction(new Notification.Action.Builder(null, "Quieter", PendingIntent.GetService(this, 2,
                new Intent(this, typeof(NightService)).SetAction(QuieterAction), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build())!
            .AddAction(new Notification.Action.Builder(null, "Save last 20 s", PendingIntent.GetService(this, 4,
                new Intent(this, typeof(NightService)).SetAction(SaveAction), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)).Build())!
            .AddAction(new Notification.Action.Builder(null, "Stop", stop).Build())!
            .Build()!;
    }

    private PendingIntent OpenApp() => PendingIntent.GetActivity(this, 0,
        new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.SingleTop), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;

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
            service.focusGranted = focusChange == AudioFocus.Gain;
            if (service.engine is { } running)
                running.FocusGranted = service.focusGranted;
        }
    }
}
