using Android.Content;
using Android.Provider;

using NotifyClassifier.App.Services;

namespace NotifyClassifier.App.Platforms.Android;

using AndroidApplication = global::Android.App.Application;

public sealed class AndroidNotificationAccess : INotificationAccess
{
    public bool HasAccess
    {
        get
        {
            var context = AndroidApplication.Context;
            var enabled = Settings.Secure.GetString(context.ContentResolver, "enabled_notification_listeners");
            var packageName = context.PackageName;
            return !string.IsNullOrWhiteSpace(packageName) &&
                enabled?.Contains(packageName, StringComparison.OrdinalIgnoreCase) == true;
        }
    }

    public void OpenSettings()
    {
        var intent = new Intent(Settings.ActionNotificationListenerSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        AndroidApplication.Context.StartActivity(intent);
    }
}
