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
    private float scale = 1;
    private float offsetX;
    private float offsetY;
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
        SemanticProperties.SetDescription(this, "Sketch canvas. Choose a shape and drag to draw. Select an object to move it, or drag its lower right handle to resize.");
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
        scale = Math.Min(e.Info.Width / (float)session.Document.Width, e.Info.Height / (float)session.Document.Height);
        offsetX = (e.Info.Width - session.Document.Width * scale) / 2;
        offsetY = (e.Info.Height - session.Document.Height * scale) / 2;
        canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale);
        canvas.ClipRect(new(0, 0, session.Document.Width, session.Document.Height));
        SketchRenderer.Draw(canvas, session.Document, IsPlaying ? Progress : session.EndState ? 1 : 0,
            ShowGrid && !IsPlaying, IsPlaying ? null : session.Selected, ShowTrigger, IsPlaying && progress == 0 ? 0.6f : 0);
        canvas.Restore();
    }
    private InkPoint Point(MotionEvent motion, int index) => new(
        Math.Clamp((motion.GetX(index) - offsetX) / scale, 0, session.Document.Width),
        Math.Clamp((motion.GetY(index) - offsetY) / scale, 0, session.Document.Height), motion.GetPressure(index));

    private void HandleTouch(object? sender, Android.Views.View.TouchEventArgs args)
    {
        var motion = args.Event;
        if (motion is null || IsPlaying || scale <= 0) return;
        args.Handled = true;
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
            if (!input.Begin(id, kind)) return;
            native?.Parent?.RequestDisallowInterceptTouchEvent(true);
            session.HandleTolerance = 24 * (ContextDensity()) / scale;
            session.Begin(Point(motion, index), kind == PointerKind.Eraser || motion.ButtonState.HasFlag(MotionEventButtonState.StylusPrimary));
        }
        else if (motion.ActionMasked == MotionEventActions.Move)
        {
            for (var p = 0; p < motion.PointerCount; p++)
            {
                if (!input.Owns(motion.GetPointerId(p))) continue;
                for (var h = 0; h < motion.HistorySize; h++)
                    session.Move(new(Math.Clamp((motion.GetHistoricalX(p, h) - offsetX) / scale, 0, session.Document.Width),
                        Math.Clamp((motion.GetHistoricalY(p, h) - offsetY) / scale, 0, session.Document.Height), motion.GetHistoricalPressure(p, h)));
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
