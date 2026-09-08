using PenSketch.Core.Drawing;

namespace PenSketch.Core.Animation;

public enum TriggerKind { Tap, Text }
public sealed record AnimationTrigger
{
    public TriggerKind Kind { get; init; }
    public float X { get; init; } = 180;
    public float Y { get; init; } = 400;
    public string Label { get; init; } = "Tap here";
}
public sealed record ElementPose
{
    public required ElementId Id { get; init; }
    public required Bounds Bounds { get; init; }
    public float Opacity { get; init; } = 1;
}
public static class Transition
{
    public const int TriggerHoldMs = 700;
    public const int EndHoldMs = 600;
    public static float Progress(double elapsedMs, int durationMs) =>
        (float)Math.Clamp((elapsedMs - TriggerHoldMs) / Math.Max(1, durationMs), 0, 1);

    public static SketchElement Interpolate(SketchElement start, ElementPose? end, float progress)
    {
        if (end is null) return start;
        var t = Math.Clamp(progress, 0, 1);
        t = t * t * (3 - 2 * t);
        float Lerp(float a, float b) => a + (b - a) * t;
        return start with
        {
            Bounds = new(Lerp(start.Bounds.X, end.Bounds.X), Lerp(start.Bounds.Y, end.Bounds.Y),
                Lerp(start.Bounds.Width, end.Bounds.Width), Lerp(start.Bounds.Height, end.Bounds.Height)),
            Opacity = Lerp(start.Opacity, end.Opacity)
        };
    }
}
