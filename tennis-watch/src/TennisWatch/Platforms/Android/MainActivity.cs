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
    private bool moved;
    private bool suppressed;
    private SwipeAxis axis;

    private static ScorePage? Page =>
        Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page as ScorePage;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Window is not { } window) return;
        WindowCompat.SetDecorFitsSystemWindows(window, false);
        new WindowInsetsControllerCompat(window, window.DecorView).Hide(WindowInsetsCompat.Type.SystemBars());
    }

    protected override void OnPause()
    {
        Page?.CancelSwipe();
        base.OnPause();
    }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        if (e is null || Page is not { } page) return base.DispatchTouchEvent(e);
        var density = Resources?.DisplayMetrics?.Density ?? 1;
        var dx = (e.GetX() - startX) / density;
        var dy = (e.GetY() - startY) / density;
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                startX = e.GetX();
                startY = e.GetY();
                moved = false;
                axis = SwipeAxis.None;
                suppressed = page.IsAnimating;
                return suppressed || base.DispatchTouchEvent(e);
            case MotionEventActions.PointerDown:
                CancelChildTouch(e);
                page.CancelSwipe();
                suppressed = true;
                return true;
            case MotionEventActions.Cancel:
                page.CancelSwipe();
                suppressed = true;
                return base.DispatchTouchEvent(e);
            case MotionEventActions.Move:
                if (suppressed) return true;
                if (!moved && Math.Max(Math.Abs(dx), Math.Abs(dy)) > MatchSwipe.TouchSlop)
                {
                    moved = true;
                    axis = MatchSwipe.Axis(dx, dy);
                    CancelChildTouch(e);
                }
                if (moved)
                {
                    page.Drag(axis, dx, dy);
                    return true;
                }
                break;
            case MotionEventActions.Up:
                if (suppressed) return true;
                if (moved)
                {
                    page.FinishSwipe(axis, dx, dy);
                    return true;
                }
                break;
        }
        return base.DispatchTouchEvent(e);
    }

    private void CancelChildTouch(MotionEvent e)
    {
        using var cancel = MotionEvent.Obtain(e) ?? throw new InvalidOperationException("Cannot cancel touch.");
        cancel.Action = MotionEventActions.Cancel;
        base.DispatchTouchEvent(cancel);
    }
}
