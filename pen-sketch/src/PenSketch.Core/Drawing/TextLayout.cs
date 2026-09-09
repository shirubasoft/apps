using SkiaSharp;

namespace PenSketch.Core.Drawing;

public sealed record FittedText(float FontSize, string[] Lines);

public static class TextLayout
{
    public const float DefaultFontSize = 20;

    public static FittedText Fit(string text, Bounds bounds, float fontSize = DefaultFontSize)
    {
        using var font = new SKFont(SKTypeface.Default, fontSize);
        var lines = Wrap(text, font, bounds.Width).ToArray();
        while ((lines.Length * font.Spacing > bounds.Height || lines.Any(line => font.MeasureText(line) > bounds.Width)) && font.Size > 1)
        {
            font.Size = Math.Max(1, font.Size - 1);
            lines = Wrap(text, font, bounds.Width).ToArray();
        }
        return new(font.Size, lines);
    }

    public static float Height(string text, float width, float fontSize = DefaultFontSize)
    {
        using var font = new SKFont(SKTypeface.Default, fontSize);
        return Math.Max(44, Wrap(text, font, width).Count() * font.Spacing);
    }

    public static SketchElement Resize(SketchElement element, float fontSize, int canvasWidth, int canvasHeight)
    {
        if (element.Kind != ElementKind.Text) return element;
        fontSize = Math.Clamp(float.IsFinite(fontSize) ? fontSize : DefaultFontSize, 1, 1024);
        using var font = new SKFont(SKTypeface.Default, fontSize);
        var widestLetter = element.Text.EnumerateRunes().Select(r => font.MeasureText(r.ToString())).DefaultIfEmpty(16).Max();
        var width = Math.Min(canvasWidth, Math.Max(element.Bounds.Width, widestLetter));
        var height = Height(element.Text, width, fontSize);
        if (height > canvasHeight)
        {
            width = canvasWidth;
            height = Height(element.Text, width, fontSize);
        }
        height = Math.Min(canvasHeight, height);
        var bounds = new Bounds(Math.Clamp(element.Bounds.X, 0, canvasWidth - width),
            Math.Clamp(element.Bounds.Y, 0, canvasHeight - height), width, height);
        return element with { Bounds = bounds, FontSize = Fit(element.Text, bounds, fontSize).FontSize };
    }

    private static IEnumerable<string> Wrap(string text, SKFont font, float width)
    {
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var rune in paragraph.EnumerateRunes())
            {
                var candidate = line + rune;
                if (line.Length > 0 && font.MeasureText(candidate) > width)
                {
                    var space = line.LastIndexOf(' ');
                    if (space > 0)
                    {
                        yield return line[..space];
                        line = line[(space + 1)..] + rune;
                    }
                    else { yield return line; line = rune.ToString(); }
                }
                else line = candidate;
            }
            yield return line;
        }
    }
}
