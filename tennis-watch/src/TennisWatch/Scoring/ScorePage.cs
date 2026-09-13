using TennisWatch.Core.Matches;

namespace TennisWatch.Scoring;

public sealed class ScorePage : ContentPage
{
    internal const int PointFontSize = 48;
    private readonly MatchFile storage = new(Path.Combine(FileSystem.AppDataDirectory, "matches.json"));
    private readonly MatchBook book;
    private readonly Grid viewport = new() { BackgroundColor = Colors.Black, IsClippedToBounds = true };
    private readonly DeletePreview deletePreview = new();
    private readonly MatchDeleteHold deleteHold = new();
    private readonly IDispatcherTimer holdTimer;
    private ScoreFace current;
    private ScoreFace incoming;
    private SwipeAction previewAction;
    private DateTimeOffset swipeStartedAt;
    private bool dragging;
    private int animationGeneration;

    public ScorePage()
    {
        BackgroundColor = Colors.Black;
        Padding = 0;
        SafeAreaEdges = SafeAreaEdges.None;
        NavigationPage.SetHasNavigationBar(this, false);
        book = storage.Load(DateTimeOffset.Now);
        storage.Save(book);
        current = CreateFace();
        incoming = CreateFace();
        incoming.InputTransparent = true;
        incoming.IsVisible = false;
        viewport.Add(incoming);
        viewport.Add(current);
        viewport.Add(deletePreview);
        Content = viewport;
        current.Render(book.Current);
        holdTimer = Dispatcher.CreateTimer();
        holdTimer.Interval = TimeSpan.FromMilliseconds(25);
        holdTimer.Tick += async (_, _) => await ConfirmDeletion();
    }

    public bool IsAnimating { get; private set; }

    public void BeginSwipe()
    {
        CancelSwipe();
        deleteHold.Reset();
    }

    private static TimeSpan GestureTime => TimeSpan.FromMilliseconds(Android.OS.SystemClock.UptimeMillis());

    private ScoreFace CreateFace() => new(side => Change(() => book.Award(side)), () => Change(book.Undo));

    private void Change(Action action)
    {
        if (IsAnimating || dragging) return;
        action();
        storage.Save(book);
        current.Render(book.Current);
    }

    public void Drag(SwipeAxis axis, float x, float y)
    {
        if (IsAnimating || deleteHold.IsConfirmed || axis == SwipeAxis.None) return;
        if (!dragging)
        {
            dragging = true;
            swipeStartedAt = DateTimeOffset.Now;
            current.ResetControls();
        }
        if (axis == SwipeAxis.Horizontal)
        {
            var action = x < 0 ? SwipeAction.NextMatch : SwipeAction.PreviousMatch;
            var blocked = action == SwipeAction.PreviousMatch && !book.HasPrevious;
            if (previewAction != action)
            {
                incoming.Render(action == SwipeAction.NextMatch
                    ? book.PreviewNext(swipeStartedAt) : book.PreviewPrevious());
                previewAction = action;
            }
            incoming.IsVisible = !blocked;
            current.TranslationX = blocked ? x * 0.18 : Math.Clamp(x, -viewport.Width, viewport.Width);
            incoming.TranslationX = current.TranslationX + (x < 0 ? viewport.Width : -viewport.Width);
        }
        else
        {
            deletePreview.Reveal(-y / MatchSwipe.DeleteDistance((float)viewport.Height));
            deleteHold.Update(MatchSwipe.IsDeleteZone(axis, x, y, (float)viewport.Height), GestureTime);
            if (!deleteHold.IsWaiting) holdTimer.Stop();
            else if (!holdTimer.IsRunning) holdTimer.Start();
        }
    }

    public async void FinishSwipe(SwipeAxis axis, float x, float y)
    {
        if (IsAnimating || deleteHold.IsConfirmed) return;
        Drag(axis, x, y);
        StopDeleteHold();
        var action = MatchSwipe.Recognize(axis, x, y);
        if (action == SwipeAction.PreviousMatch && !book.HasPrevious) action = SwipeAction.None;
        var generation = ++animationGeneration;
        IsAnimating = true;
        try
        {
            switch (action)
            {
                case SwipeAction.NextMatch:
                case SwipeAction.PreviousMatch:
                    var destination = action == SwipeAction.NextMatch ? -viewport.Width : viewport.Width;
                    await Task.WhenAll(Move(current, destination, 0, 150), Move(incoming, 0, 0, 150));
                    if (generation != animationGeneration) return;
                    if (action == SwipeAction.NextMatch) book.NextMatch(swipeStartedAt);
                    else book.PreviousMatch();
                    storage.Save(book);
                    (current, incoming) = (incoming, current);
                    current.InputTransparent = false;
                    incoming.InputTransparent = true;
                    current.Render(book.Current);
                    break;
                default:
                    var restingX = previewAction == SwipeAction.NextMatch ? viewport.Width : -viewport.Width;
                    await Task.WhenAll(Move(current, 0, 0, 160), Move(incoming, restingX, 0, 160),
                        Move(deletePreview, 0, deletePreview.HiddenOffset, 160));
                    break;
            }
        }
        finally
        {
            if (generation == animationGeneration) ResetMotion();
        }
    }

    public void CancelSwipe()
    {
        animationGeneration++;
        StopDeleteHold();
        current.CancelAnimations();
        incoming.CancelAnimations();
        deletePreview.CancelAnimations();
        if (deleteHold.IsConfirmed) current.Render(book.Current);
        ResetMotion();
    }

    private void StopDeleteHold()
    {
        holdTimer.Stop();
        deleteHold.Cancel();
    }

    private async Task ConfirmDeletion()
    {
        if (!deleteHold.TryConfirm(GestureTime)) return;
        holdTimer.Stop();
        IsAnimating = true;
        var generation = ++animationGeneration;
        try
        {
            book.DeleteCurrent(DateTimeOffset.Now);
            storage.Save(book);
            if (Vibration.Default.IsSupported) Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(80));
            await Task.WhenAll(Move(current, 0, viewport.Height * 0.3, 180, Easing.CubicIn),
                Fade(current, 0, 180, Easing.CubicIn));
            if (generation != animationGeneration) return;
            current.Render(book.Current);
            current.TranslationY = 0;
            await Task.WhenAll(Fade(current, 1, 220, Easing.CubicOut),
                Move(deletePreview, 0, deletePreview.HiddenOffset, 220));
        }
        finally
        {
            if (generation == animationGeneration) ResetMotion();
        }
    }

    private void ResetMotion()
    {
        current.TranslationX = current.TranslationY = 0;
        incoming.TranslationX = incoming.TranslationY = 0;
        current.Opacity = incoming.Opacity = 1;
        incoming.IsVisible = false;
        deletePreview.Reveal(0);
        previewAction = SwipeAction.None;
        dragging = IsAnimating = false;
        current.ResetControls();
    }

    private static Task Move(View view, double x, double y, uint milliseconds, Easing? easing = null)
    {
        var duration = MotionDuration(milliseconds);
        if (duration == 0)
        {
            view.TranslationX = x;
            view.TranslationY = y;
            return Task.CompletedTask;
        }
        return view.TranslateToAsync(x, y, duration, easing ?? Easing.CubicOut);
    }

    private static Task Fade(View view, double opacity, uint milliseconds, Easing easing)
    {
        var duration = MotionDuration(milliseconds);
        if (duration == 0)
        {
            view.Opacity = opacity;
            return Task.CompletedTask;
        }
        return view.FadeToAsync(opacity, duration, easing);
    }

    private static uint MotionDuration(uint milliseconds)
    {
        var scale = Android.Provider.Settings.Global.GetFloat(Android.App.Application.Context.ContentResolver,
            Android.Provider.Settings.Global.AnimatorDurationScale, 1);
        return (uint)(milliseconds * Math.Clamp(scale, 0, 2));
    }
}
