using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using TennisWatch.Core.Matches;
using TennisWatch.Scoring;

namespace TennisWatch;

[Activity(Theme = "@style/TennisTheme", MainLauncher = true, Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
    private float startX;
    private float startY;
    private bool swiping;
    private bool moved;
    private bool multiplePointers;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Window is not { } window) return;
        WindowCompat.SetDecorFitsSystemWindows(window, false);
        new WindowInsetsControllerCompat(window, window.DecorView).Hide(WindowInsetsCompat.Type.SystemBars());
    }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e is null) return base.DispatchTouchEvent(e);
        var density = Resources?.DisplayMetrics?.Density ?? 1;
        var dx = e.GetX() - startX;
        var dy = e.GetY() - startY;
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                startX = e.GetX();
                startY = e.GetY();
                swiping = false;
                moved = false;
                multiplePointers = false;
                break;
            case MotionEventActions.PointerDown:
                multiplePointers = true;
                break;
            case MotionEventActions.Cancel:
                moved = true;
                swiping = false;
                break;
            case MotionEventActions.Move:
                if (!moved && Math.Max(Math.Abs(dx), Math.Abs(dy)) > 12 * density)
                {
                    moved = true;
                    using var cancel = MotionEvent.Obtain(e) ?? throw new InvalidOperationException("Cannot cancel touch.");
                    cancel.Action = MotionEventActions.Cancel;
                    base.DispatchTouchEvent(cancel);
                }
                swiping |= MatchSwipe.Recognize(dx, dy, density) != SwipeAction.None;
                if (moved) return true;
                break;
            case MotionEventActions.Up:
                if (!multiplePointers && swiping &&
                    Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is ScorePage page)
                    page.Navigate(MatchSwipe.Recognize(dx, dy, density));
                if (moved || multiplePointers) return true;
                break;
        }
        return base.DispatchTouchEvent(e);
    }
}
