using Android.Views;
using PenSketch.Core.Drawing;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PenSketch.Drawing;

public sealed class PenCanvas : SKCanvasView
{
    private readonly EditorSession session;
    private readonly PenInput input = new();
    private Android.Views.View? native;
    private CanvasTransform transform = new(1, 0, 0);
    private CanvasTransform navigationStart;
    private InkPoint navigationAnchor;
    private float navigationDistance;
    private bool navigating;
    private bool customView;
    private (int Width, int Height, int DocumentWidth, int DocumentHeight) dimensions;
    private float progress;
    public bool IsPlaying { get; set; }
    public bool ShowTrigger { get; set; }
    public bool ShowGrid { get; set; } = true;
    public bool PenOnly { get => input.PenOnly; set => input.PenOnly = value; }
    public event Action? GestureCompleted;
    public float Progress { get => progress; set { progress = value; InvalidateSurface(); } }
    public PenCanvas(EditorSession session)
    {
        this.session = session;
        PaintSurface += Paint;
        session.Changed += InvalidateSurface;
        HandlerChanged += Attach;
        HandlerChanging += (_, _) => Detach();
        SemanticProperties.SetDescription(this, "Sketch canvas. Choose a shape and drag to draw. Select an object to move it, or drag its lower right handle to resize. Use two fingers to zoom and pan. Fit shows the whole canvas.");
    }
    private void Attach(object? sender, EventArgs e)
    {
        native = Handler?.PlatformView as Android.Views.View;
        if (native is not null) native.Touch += HandleTouch;
    }
    private void Detach()
    {
        if (native is not null) native.Touch -= HandleTouch;
        native = null;
        input.End();
        session.Cancel();
    }
    private void Paint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SkiaSharp.SKColor.Parse("#E4E9F0"));
        var nextDimensions = (e.Info.Width, e.Info.Height, session.Document.Width, session.Document.Height);
        if (dimensions != nextDimensions) { dimensions = nextDimensions; customView = false; }
        if (!customView) transform = CanvasTransform.Fit(e.Info.Width, e.Info.Height, session.Document.Width, session.Document.Height);
        canvas.Save();
        canvas.Translate(transform.X, transform.Y);
        canvas.Scale(transform.Scale);
        canvas.ClipRect(new(0, 0, session.Document.Width, session.Document.Height));
        SketchRenderer.Draw(canvas, session.Document, IsPlaying ? Progress : session.EndState ? 1 : 0,
            ShowGrid && !IsPlaying, IsPlaying ? null : session.Selected, ShowTrigger, IsPlaying && progress == 0 ? 0.6f : 0,
            ContextDensity() / transform.Scale);
        canvas.Restore();
    }
    public void ResetView()
    {
        customView = false; navigating = false; input.End(); session.Cancel(); InvalidateSurface();
    }
    private InkPoint Point(MotionEvent motion, int index) => new(
        Math.Clamp((motion.GetX(index) - transform.X) / transform.Scale, 0, session.Document.Width),
        Math.Clamp((motion.GetY(index) - transform.Y) / transform.Scale, 0, session.Document.Height), motion.GetPressure(index));

    private bool Navigate(MotionEvent motion)
    {
        if (motion.ActionMasked == MotionEventActions.Down) navigating = false;
        if (motion.PointerCount >= 2 && motion.GetToolType(0) == MotionEventToolType.Finger && motion.GetToolType(1) == MotionEventToolType.Finger)
        {
            var x = (motion.GetX(0) + motion.GetX(1)) / 2;
            var y = (motion.GetY(0) + motion.GetY(1)) / 2;
            var distance = MathF.Max(1, MathF.Sqrt(MathF.Pow(motion.GetX(0) - motion.GetX(1), 2) + MathF.Pow(motion.GetY(0) - motion.GetY(1), 2)));
            if (!navigating)
            {
                session.Cancel(); input.End(); navigating = true;
                navigationStart = transform; navigationDistance = distance;
                navigationAnchor = transform.ToDocument(x, y);
                native?.Parent?.RequestDisallowInterceptTouchEvent(true);
            }
            if (motion.ActionMasked == MotionEventActions.Move)
            {
                var fit = CanvasTransform.Fit(dimensions.Width, dimensions.Height, session.Document.Width, session.Document.Height);
                var scale = Math.Clamp(navigationStart.Scale * distance / navigationDistance, fit.Scale, fit.Scale * 8);
                transform = navigationStart.Navigate(navigationAnchor, x, y, scale);
                customView = true; InvalidateSurface();
            }
        }
        if (navigating && motion.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel)
            native?.Parent?.RequestDisallowInterceptTouchEvent(false);
        return navigating;
    }

    private void HandleTouch(object? sender, Android.Views.View.TouchEventArgs args)
    {
        var motion = args.Event;
        if (motion is null || transform.Scale <= 0) return;
        args.Handled = true;
        if (Navigate(motion) || IsPlaying) return;
        var index = motion.ActionIndex;
        var id = motion.GetPointerId(index);
        if (motion.ActionMasked is MotionEventActions.Down or MotionEventActions.PointerDown)
        {
            var kind = motion.GetToolType(index) switch
            {
                MotionEventToolType.Stylus => PointerKind.Pen,
                MotionEventToolType.Eraser => PointerKind.Eraser,
                MotionEventToolType.Mouse => PointerKind.Mouse,
                _ => PointerKind.Finger
            };
            var point = transform.ToDocument(motion.GetX(index), motion.GetY(index));
            if (point.X < 0 || point.X > session.Document.Width || point.Y < 0 || point.Y > session.Document.Height) return;
            if (!input.Begin(id, kind)) return;
            native?.Parent?.RequestDisallowInterceptTouchEvent(true);
            session.HandleTolerance = 24 * (ContextDensity()) / transform.Scale;
            session.Begin(Point(motion, index), kind == PointerKind.Eraser || motion.ButtonState.HasFlag(MotionEventButtonState.StylusPrimary));
        }
        else if (motion.ActionMasked == MotionEventActions.Move)
        {
            for (var p = 0; p < motion.PointerCount; p++)
            {
                if (!input.Owns(motion.GetPointerId(p))) continue;
                for (var h = 0; h < motion.HistorySize; h++)
                    session.Move(new(Math.Clamp((motion.GetHistoricalX(p, h) - transform.X) / transform.Scale, 0, session.Document.Width),
                        Math.Clamp((motion.GetHistoricalY(p, h) - transform.Y) / transform.Scale, 0, session.Document.Height), motion.GetHistoricalPressure(p, h)));
                session.Move(Point(motion, p));
            }
        }
        else if (motion.ActionMasked is MotionEventActions.Up or MotionEventActions.PointerUp)
        {
            if (!input.Owns(id)) return;
            if (((int)motion.Flags & 0x20) != 0) session.Cancel();
            else session.End(Point(motion, index));
            input.End();
            GestureCompleted?.Invoke();
            native?.Parent?.RequestDisallowInterceptTouchEvent(false);
        }
        else if (motion.ActionMasked == MotionEventActions.Cancel)
        {
            session.Cancel(); input.End(); native?.Parent?.RequestDisallowInterceptTouchEvent(false);
        }
    }
    private float ContextDensity() => native?.Resources?.DisplayMetrics?.Density ?? 1;
}
