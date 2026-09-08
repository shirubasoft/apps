namespace PenSketch.Core.Drawing;

public static class HitTest
{
    public static SketchElement? Element(IEnumerable<SketchElement> elements, InkPoint point, float tolerance = 8) =>
        elements.Reverse().FirstOrDefault(element => Contains(element, point, tolerance));

    public static bool Contains(SketchElement element, InkPoint point, float tolerance)
    {
        if (!element.Bounds.Contains(point.X, point.Y, tolerance)) return false;
        if (element.Kind != ElementKind.Ink) return true;
        var b = element.Bounds;
        var points = element.Points;
        for (var i = 0; i < points.Length; i++)
        {
            var a = new InkPoint(b.X + points[i].X * b.Width, b.Y + points[i].Y * b.Height);
            var next = points[Math.Min(i + 1, points.Length - 1)];
            var c = new InkPoint(b.X + next.X * b.Width, b.Y + next.Y * b.Height);
            var dx = c.X - a.X;
            var dy = c.Y - a.Y;
            var length = dx * dx + dy * dy;
            var t = length == 0 ? 0 : Math.Clamp(((point.X - a.X) * dx + (point.Y - a.Y) * dy) / length, 0, 1);
            if (MathF.Pow(point.X - a.X - t * dx, 2) + MathF.Pow(point.Y - a.Y - t * dy, 2) <= tolerance * tolerance) return true;
        }
        return false;
    }
}
