using PenSketch.Core.Drawing;

namespace PenSketch.Core.Documents;

public sealed record ViewportReference
{
    public required int Width { get; init; }
    public required int Height { get; init; }
}

public sealed record CanvasViewport
{
    public required string Name { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public static IReadOnlyList<CanvasViewport> Presets { get; } =
    [
        new() { Name = "Mobile", Width = 360, Height = 640 },
        new() { Name = "Large mobile", Width = 412, Height = 915 },
        new() { Name = "Tablet", Width = 768, Height = 1024 },
        new() { Name = "Laptop", Width = 1366, Height = 768 },
        new() { Name = "Desktop", Width = 1920, Height = 1080 }
    ];

    public string Label => $"{Name} · {Width} × {Height}";
    public static bool IsSupported(int width, int height) => Presets.Any(p =>
        (p.Width == width && p.Height == height) || (p.Width == height && p.Height == width));
    public static string Describe(int width, int height) =>
        $"{Presets.FirstOrDefault(p => (p.Width == width && p.Height == height) || (p.Width == height && p.Height == width))?.Name ?? "Canvas"} · {width} × {height}";

    public static SketchDocument Resize(SketchDocument document, int width, int height)
    {
        if (!IsSupported(width, height)) throw new ArgumentOutOfRangeException(nameof(width), "Choose a supported canvas viewport.");
        if (width == document.Width && height == document.Height) return document;
        var reference = document.ReferenceViewport ?? new ViewportReference { Width = document.Width, Height = document.Height };
        var currentScale = Math.Min(document.Width / (float)reference.Width, document.Height / (float)reference.Height);
        var nextScale = Math.Min(width / (float)reference.Width, height / (float)reference.Height);
        var scale = nextScale / currentScale;
        var dx = (width - document.Width * scale) / 2;
        var dy = (height - document.Height * scale) / 2;
        Bounds Transform(Bounds bounds) => new(bounds.X * scale + dx, bounds.Y * scale + dy, bounds.Width * scale, bounds.Height * scale);
        return document with
        {
            Width = width, Height = height,
            ReferenceViewport = reference,
            Elements = document.Elements.Select(e => e with
            {
                Bounds = Transform(e.Bounds), FontSize = e.FontSize * scale, StrokeWidth = e.StrokeWidth * scale
            }).ToArray(),
            EndPoses = document.EndPoses.Select(p => p with
            {
                Bounds = Transform(p.Bounds), FontSize = p.FontSize * scale
            }).ToArray(),
            Trigger = document.Trigger is { } trigger ? trigger with { X = trigger.X * scale + dx, Y = trigger.Y * scale + dy } : null
        };
    }
}
