namespace PenSketch.Core.Drawing;

public readonly record struct CanvasTransform(float Scale, float X, float Y)
{
    public InkPoint ToDocument(float x, float y, float pressure = 0.5f) => new((x - X) / Scale, (y - Y) / Scale, pressure);
    public static CanvasTransform Fit(float viewWidth, float viewHeight, int documentWidth, int documentHeight)
    {
        var scale = Math.Min(viewWidth / documentWidth, viewHeight / documentHeight);
        return new(scale, (viewWidth - documentWidth * scale) / 2, (viewHeight - documentHeight * scale) / 2);
    }
    public CanvasTransform Navigate(InkPoint anchor, float x, float y, float scale) =>
        new(scale, x - anchor.X * scale, y - anchor.Y * scale);
}
