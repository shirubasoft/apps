using TennisWatch.Core.Matches;

namespace TennisWatch.Scoring;

public sealed class ScorePage : ContentPage
{
    internal const int PointFontSize = 48;
    private readonly MatchFile storage = new(Path.Combine(FileSystem.AppDataDirectory, "matches.json"));
    private readonly MatchBook book;
    private readonly Grid viewport = new() { BackgroundColor = Colors.Black, IsClippedToBounds = true };
    private readonly AbsoluteLayout deleteLayer = new() { InputTransparent = true, IsVisible = false };
    private readonly Image trash = new() { Source = "trash.png", Aspect = Aspect.AspectFit, AutomationId = "delete-preview" };
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
        deleteLayer.Add(trash);
        viewport.Add(deleteLayer);
        viewport.Add(incoming);
        viewport.Add(current);
        viewport.SizeChanged += (_, _) => ArrangeTrash();
        Content = viewport;
        current.Render(book.Current);
    }

    public bool IsAnimating { get; private set; }

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
        if (IsAnimating || axis == SwipeAxis.None) return;
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
            current.TranslationY = Math.Clamp(y, -viewport.Height, 0);
            var progress = Math.Clamp(-y / MatchSwipe.DeleteDistance((float)viewport.Height), 0, 1);
            deleteLayer.IsVisible = y < 0;
            trash.Opacity = 0.45 + 0.55 * progress;
            trash.Scale = 0.8 + 0.2 * progress;
        }
    }

    public async void FinishSwipe(SwipeAxis axis, float x, float y)
    {
        if (IsAnimating) return;
        Drag(axis, x, y);
        var action = MatchSwipe.Recognize(axis, x, y, (float)viewport.Height);
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
                case SwipeAction.DeleteMatch:
                    await Move(current, 0, -viewport.Height, 130);
                    if (generation != animationGeneration) return;
                    book.DeleteCurrent(DateTimeOffset.Now);
                    storage.Save(book);
                    current.Render(book.Current);
                    deleteLayer.IsVisible = false;
                    current.TranslationY = viewport.Height;
                    await Move(current, 0, 0, 180);
                    break;
                default:
                    var restingX = previewAction == SwipeAction.NextMatch ? viewport.Width : -viewport.Width;
                    await Task.WhenAll(Move(current, 0, 0, 160), Move(incoming, restingX, 0, 160));
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
        current.CancelAnimations();
        incoming.CancelAnimations();
        ResetMotion();
    }

    private void ResetMotion()
    {
        current.TranslationX = current.TranslationY = 0;
        incoming.TranslationX = incoming.TranslationY = 0;
        incoming.IsVisible = false;
        deleteLayer.IsVisible = false;
        previewAction = SwipeAction.None;
        dragging = IsAnimating = false;
        current.ResetControls();
    }

    private Task Move(View view, double x, double y, uint milliseconds)
    {
        var scale = Android.Provider.Settings.Global.GetFloat(Android.App.Application.Context.ContentResolver,
            Android.Provider.Settings.Global.AnimatorDurationScale, 1);
        if (scale <= 0)
        {
            view.TranslationX = x;
            view.TranslationY = y;
            return Task.CompletedTask;
        }
        return view.TranslateToAsync(x, y, (uint)(milliseconds * Math.Min(scale, 2)), Easing.CubicOut);
    }

    private void ArrangeTrash()
    {
        var size = Math.Min(viewport.Width, viewport.Height);
        AbsoluteLayout.SetLayoutBounds(trash, new Rect((viewport.Width - 48) / 2,
            (viewport.Height - size) / 2 + size * 0.8 - 24, 48, 48));
    }
}
