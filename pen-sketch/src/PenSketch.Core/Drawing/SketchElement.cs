namespace PenSketch.Core.Drawing;

public enum ElementKind { Rectangle, Ellipse, Squircle, Text, Ink }
public enum DrawingTool { Select, Pen, Rectangle, Ellipse, Squircle, Text, Eraser, Trigger, Square }
public readonly record struct ElementId(Guid Value)
{
    public static ElementId New() => new(Guid.NewGuid());
}
public readonly record struct InkPoint(float X, float Y, float Pressure = 0.5f);
public readonly record struct Bounds(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public bool Contains(float x, float y, float tolerance = 0) =>
        x >= X - tolerance && y >= Y - tolerance && x <= Right + tolerance && y <= Bottom + tolerance;
    public Bounds Move(float dx, float dy) => this with { X = X + dx, Y = Y + dy };
    public Bounds Resize(float dx, float dy) => this with { Width = Math.Max(16, Width + dx), Height = Math.Max(16, Height + dy) };
    public static Bounds Between(InkPoint a, InkPoint b) => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(16, Math.Abs(b.X - a.X)), Math.Max(16, Math.Abs(b.Y - a.Y)));
}
public sealed record SketchElement
{
    public required ElementId Id { get; init; }
    public required ElementKind Kind { get; init; }
    public required Bounds Bounds { get; init; }
    public float Opacity { get; init; } = 1;
    public string Text { get; init; } = "";
    public InkPoint[] Points { get; init; } = [];
    public float StrokeWidth { get; init; } = 2.5f;
}
