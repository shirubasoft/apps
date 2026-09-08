namespace PenSketch.Core.Drawing;

public static class InkStroke
{
    public static SketchElement Create(ElementId id, IReadOnlyList<InkPoint> points)
    {
        var x = points.Min(p => p.X);
        var y = points.Min(p => p.Y);
        var bounds = new Bounds(x, y, Math.Max(1, points.Max(p => p.X) - x), Math.Max(1, points.Max(p => p.Y) - y));
        return new SketchElement
        {
            Id = id, Kind = ElementKind.Ink, Bounds = bounds,
            Points = points.Select(p => new InkPoint((p.X - x) / bounds.Width, (p.Y - y) / bounds.Height, Math.Clamp(p.Pressure, 0.1f, 1))).ToArray()
        };
    }
}
