using Android.App;
using Android.Content.PM;
using Android.Views;

namespace QuietNight;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
    private bool swallowing;

    /// <summary>
    /// Sees every touch first; returning true keeps the whole gesture from reaching the page, as when a touch only wakes
    /// a dimmed night screen.
    /// </summary>
    public static Func<bool>? Intercept { get; set; }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e?.Action == MotionEventActions.Down) swallowing = Intercept?.Invoke() == true;
        if (!swallowing) return base.DispatchTouchEvent(e);
        if (e?.Action is MotionEventActions.Up or MotionEventActions.Cancel) swallowing = false;
        return true;
    }
}
