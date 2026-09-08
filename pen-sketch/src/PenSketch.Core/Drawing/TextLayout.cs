using SkiaSharp;

namespace PenSketch.Core.Drawing;

public sealed record FittedText(float FontSize, string[] Lines);

public static class TextLayout
{
    public const float DefaultFontSize = 20;

    public static FittedText Fit(string text, Bounds bounds)
    {
        using var font = new SKFont(SKTypeface.Default, DefaultFontSize);
        var lines = Wrap(text, font, bounds.Width).ToArray();
        while (lines.Length * font.Spacing > bounds.Height && font.Size > 1)
        {
            font.Size = Math.Max(1, font.Size - 1);
            lines = Wrap(text, font, bounds.Width).ToArray();
        }
        return new(font.Size, lines);
    }

    public static float Height(string text, float width)
    {
        using var font = new SKFont(SKTypeface.Default, DefaultFontSize);
        return Math.Max(44, Wrap(text, font, width).Count() * font.Spacing);
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
