using PenSketch.Core.Animation;
using PenSketch.Core.Documents;
using SkiaSharp;

namespace PenSketch.Core.Drawing;

public static class SketchRenderer
{
    public static readonly SKColor Ink = SKColor.Parse("#252A31");
    public static readonly SKColor Accent = SKColor.Parse("#246BCE");
    public static void Draw(SKCanvas canvas, SketchDocument document, float progress = 0, bool grid = false,
        ElementId? selected = null, bool showTrigger = false, float triggerPulse = 0, float selectionScale = 1)
    {
        canvas.Clear(SKColors.White);
        if (grid)
        {
            using var dots = new SKPaint { Color = SKColor.Parse("#DBE0E6"), IsAntialias = true };
            for (var y = 16; y < document.Height; y += 16)
                for (var x = 16; x < document.Width; x += 16) canvas.DrawCircle(x, y, 0.7f, dots);
        }
        foreach (var element in document.At(progress)) DrawElement(canvas, element);
        if (selected is { } id)
        {
            var element = document.At(progress).FirstOrDefault(e => e.Id == id);
            if (element is not null) DrawSelection(canvas, element.Bounds, selectionScale);
        }
        if (showTrigger && document.Trigger is { } trigger) DrawTrigger(canvas, trigger, triggerPulse, document.Width, document.Height);
    }
    private static void DrawElement(SKCanvas canvas, SketchElement element)
    {
        var b = element.Bounds;
        var alpha = (byte)(Math.Clamp(element.Opacity, 0, 1) * 255);
        using var paint = new SKPaint { Color = Ink.WithAlpha(alpha), IsAntialias = true, StrokeWidth = element.StrokeWidth, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
        var rect = new SKRect(b.X, b.Y, b.Right, b.Bottom);
        switch (element.Kind)
        {
            case ElementKind.Rectangle: canvas.DrawRect(rect, paint); break;
            case ElementKind.Ellipse: canvas.DrawOval(rect, paint); break;
            case ElementKind.Squircle:
                using (var path = Squircle(b)) canvas.DrawPath(path, paint);
                break;
            case ElementKind.Text:
                paint.Style = SKPaintStyle.Fill;
                var layout = TextLayout.Fit(element.Text, b, element.FontSize);
                using (var font = new SKFont(SKTypeface.Default, layout.FontSize))
                {
                    canvas.Save();
                    canvas.ClipRect(rect);
                    var lines = layout.Lines;
                    var baseline = b.Y - font.Metrics.Ascent;
                    foreach (var line in lines)
                    {
                        canvas.DrawText(line, b.X, baseline, SKTextAlign.Left, font, paint);
                        baseline += font.Spacing;
                    }
                    canvas.Restore();
                }
                break;
            case ElementKind.Ink:
                for (var i = 0; i < element.Points.Length; i++)
                {
                    var a = element.Points[Math.Max(0, i - 1)];
                    var c = element.Points[i];
                    paint.StrokeWidth = element.StrokeWidth * (0.45f + c.Pressure);
                    canvas.DrawLine(b.X + a.X * b.Width, b.Y + a.Y * b.Height, b.X + c.X * b.Width, b.Y + c.Y * b.Height, paint);
                    if (i == 0) canvas.DrawCircle(b.X + c.X * b.Width, b.Y + c.Y * b.Height, paint.StrokeWidth / 3, paint);
                }
                break;
        }
    }
    private static SKPath Squircle(Bounds b)
    {
        using var path = new SKPathBuilder();
        for (var i = 0; i <= 80; i++)
        {
            var angle = i * MathF.Tau / 80;
            var c = MathF.Cos(angle);
            var s = MathF.Sin(angle);
            var x = b.X + b.Width / 2 + b.Width / 2 * MathF.CopySign(MathF.Sqrt(MathF.Abs(c)), c);
            var y = b.Y + b.Height / 2 + b.Height / 2 * MathF.CopySign(MathF.Sqrt(MathF.Abs(s)), s);
            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        path.Close();
        return path.Detach();
    }
    private static void DrawSelection(SKCanvas canvas, Bounds b, float scale)
    {
        using var stroke = new SKPaint { Color = Accent, Style = SKPaintStyle.Stroke, StrokeWidth = scale, IsAntialias = true };
        canvas.DrawRect(new SKRect(b.X - 4 * scale, b.Y - 4 * scale, b.Right + 4 * scale, b.Bottom + 4 * scale), stroke);
        using var fill = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawCircle(b.Right, b.Bottom, 8 * scale, fill);
        stroke.StrokeWidth = 2 * scale;
        canvas.DrawCircle(b.Right, b.Bottom, 8 * scale, stroke);
        canvas.DrawLine(b.Right - 3 * scale, b.Bottom + 3 * scale, b.Right + 3 * scale, b.Bottom - 3 * scale, stroke);
    }
    private static void DrawTrigger(SKCanvas canvas, AnimationTrigger trigger, float pulse, int width, int height)
    {
        using var paint = new SKPaint { Color = Accent, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
        canvas.DrawCircle(trigger.X, trigger.Y, 12 + pulse * 14, paint);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawCircle(trigger.X, trigger.Y, 4, paint);
        var label = (trigger.Kind == TriggerKind.Tap ? "Tap: " : "Type: ") + trigger.Label;
        using var font = new SKFont(SKTypeface.Default, 13);
        var labelWidth = Math.Min(width - 16, font.MeasureText(label) + 16);
        var x = Math.Clamp(trigger.X - labelWidth / 2, 8, width - labelWidth - 8);
        var y = Math.Clamp(trigger.Y > height - 60 ? trigger.Y - 44 : trigger.Y + 24, 8, height - 36);
        canvas.DrawRoundRect(new SKRect(x, y, x + labelWidth, y + 28), 6, 6, paint);
        paint.Color = SKColors.White;
        canvas.Save();
        canvas.ClipRect(new SKRect(x + 5, y, x + labelWidth - 5, y + 28));
        canvas.DrawText(label, x + 8, y + 19, SKTextAlign.Left, font, paint);
        canvas.Restore();
    }
}
