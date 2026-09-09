using PenSketch.Core.Animation;
using PenSketch.Core.Documents;
using PenSketch.Core.Drawing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;

namespace PenSketch.Core.Export;

public static class SketchExport
{
    public static byte[] Png(SketchDocument document, float progress = 0, bool showTrigger = false, int scale = 2)
        => Raster(document, progress, showTrigger, document.Width * scale, document.Height * scale);

    private static byte[] Raster(SketchDocument document, float progress, bool showTrigger, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Scale(width / (float)document.Width, height / (float)document.Height);
        SketchRenderer.Draw(surface.Canvas, document, progress, showTrigger: showTrigger);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static void Gif(SketchDocument document, Stream output, CancellationToken cancellationToken = default)
    {
        if (document.Trigger is null || document.EndPoses.Length == 0)
            throw new InvalidOperationException("Set a trigger and an end state before exporting an animation.");
        cancellationToken.ThrowIfCancellationRequested();
        var scale = Math.Min(1, 960f / Math.Max(document.Width, document.Height));
        var width = (int)Math.Round(document.Width * scale);
        var height = (int)Math.Round(document.Height * scale);
        using var animation = Image.Load<Rgba32>(Raster(document, 0, true, width, height));
        animation.Metadata.GetGifMetadata().RepeatCount = 0;
        animation.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = Transition.TriggerHoldMs / 10;
        const int frameMs = 50;
        var frames = (int)Math.Ceiling(document.DurationMs / (double)frameMs);
        for (var i = 0; i <= frames; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var frame = Image.Load<Rgba32>(Raster(document, i / (float)frames, true, width, height));
            frame.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = i == frames ? Transition.EndHoldMs / 10 : frameMs / 10;
            animation.Frames.AddFrame(frame.Frames.RootFrame);
        }
        animation.SaveAsGif(output, new GifEncoder());
    }

    public static string Describe(SketchDocument document) =>
        $"UI sketch: {document.Name}\nCanvas: {document.Width} x {document.Height}\n" +
        (document.Trigger is { } trigger
            ? $"Trigger: {trigger.Kind} at ({trigger.X:0}, {trigger.Y:0}), {trigger.Label}\nTransition: {document.DurationMs} ms, smooth easing.\n"
            : "") + string.Join("\n", document.Elements.Select(element =>
            $"{element.Kind}{(element.Text.Length > 0 ? $" \"{element.Text}\"" : "")}: " +
            $"x={element.Bounds.X:0}, y={element.Bounds.Y:0}, width={element.Bounds.Width:0}, height={element.Bounds.Height:0}" +
            (element.Kind == ElementKind.Text ? $", fontSize={element.FontSize:0.##}" : "") +
            (document.EndPoses.FirstOrDefault(p => p.Id == element.Id) is { } end
                ? $" -> x={end.Bounds.X:0}, y={end.Bounds.Y:0}, width={end.Bounds.Width:0}, height={end.Bounds.Height:0}, opacity={end.Opacity:0.##}" +
                  (element.Kind == ElementKind.Text ? $", fontSize={end.FontSize ?? element.FontSize:0.##}" : "")
                : "")));
}
