using Android.App;
using Android.Content;
using Android.Service.Notification;

using NotifyClassifier.App.Services;
using NotifyClassifier.Core;
using NotifyClassifier.Storage;

namespace NotifyClassifier.App.Platforms.Android;

[Service(
    Name = "dev.danielreis.notifyclassifier.NotificationCaptureService",
    Label = "Notify Classifier",
    Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE",
    Exported = true)]
[IntentFilter(["android.service.notification.NotificationListenerService"])]
public sealed class NotificationCaptureService : NotificationListenerService
{
    public override void OnListenerConnected()
    {
        base.OnListenerConnected();
        AppRuntime.GetRequiredService<IRetryScheduler>().SchedulePeriodic();
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        base.OnNotificationPosted(sbn);
        if (sbn is null ||
            string.IsNullOrWhiteSpace(sbn.PackageName) ||
            string.Equals(sbn.PackageName, PackageName, StringComparison.Ordinal))
        {
            return;
        }

        _ = CaptureAsync(sbn);
    }

    private static async Task CaptureAsync(StatusBarNotification statusBarNotification)
    {
        var repository = AppRuntime.GetRequiredService<INotificationRepository>();
        var packageName = statusBarNotification.PackageName ?? string.Empty;
        var selection = await repository.GetEnabledSelectionAsync(packageName);
        if (selection is null)
        {
            return;
        }

        var schema = await repository.GetSchemaAsync(selection.SchemaId);
        if (schema is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var notification = statusBarNotification.Notification;
        var stored = new StoredNotification(
            Guid.NewGuid().ToString(),
            selection.PackageName,
            selection.DisplayName,
            notification?.Extras?.GetCharSequence(Notification.ExtraTitle)?.ToString(),
            notification?.Extras?.GetCharSequence(Notification.ExtraText)?.ToString(),
            DateTimeOffset.FromUnixTimeMilliseconds(statusBarNotification.PostTime),
            schema.Id,
            schema.Name,
            schema.JsonSchema,
            NotificationState.Pending,
            0,
            now,
            null,
            null,
            now);

        await repository.EnqueueAsync(stored);
        AppRuntime.GetRequiredService<IRetryScheduler>().ScheduleSoon();
        await AppRuntime.GetRequiredService<RetryCoordinator>().ProcessDueAsync();
    }
}
