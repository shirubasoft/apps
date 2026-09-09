using System.Text.Json;
using PenSketch.Core.Documents;
using PenSketch.Core.Drawing;
using PenSketch.Core.Export;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PenSketch.Tests;

public class ViewportTests
{
    public static IEnumerable<object[]> Viewports() => CanvasViewport.Presets.SelectMany(p => new[]
    {
        new object[] { p.Width, p.Height }, new object[] { p.Height, p.Width }
    });

    [Theory]
    [MemberData(nameof(Viewports))]
    public async Task ViewportFitsArtworkAndBothPosesThenSurvivesRestart(int width, int height)
    {
        var original = ExampleSketch.Create();
        var changed = CanvasViewport.Resize(original, width, height);
        var scale = Math.Min(width / 360f, height / 640f);
        Assert.Equal(width, changed.Width); Assert.Equal(height, changed.Height);
        Assert.Contains($"{width} × {height}", CanvasViewport.Describe(width, height));
        foreach (var (before, after) in original.At(1).Zip(changed.At(1)))
        {
            Assert.Equal(before.Bounds.Width * scale, after.Bounds.Width, 3);
            Assert.Equal(before.Bounds.Height * scale, after.Bounds.Height, 3);
            Assert.Equal(before.FontSize * scale, after.FontSize, 3);
            Assert.Equal(before.Opacity, after.Opacity);
            Assert.Equal(before.Points, after.Points);
            Assert.InRange(after.Bounds.Right, 0, width); Assert.InRange(after.Bounds.Bottom, 0, height);
        }
        Assert.Equal(original.Trigger!.Label, changed.Trigger!.Label);
        Assert.Equal(original.Trigger.Kind, changed.Trigger.Kind);
        Assert.InRange(changed.Trigger.X, 0, width); Assert.InRange(changed.Trigger.Y, 0, height);
        var directory = Path.Combine(Path.GetTempPath(), "pen-sketch-tests", Guid.NewGuid().ToString());
        try
        {
            var store = new DocumentStore(Path.Combine(directory, "sketch.json"));
            await store.SaveAsync(changed);
            var loaded = await store.LoadAsync();
            Assert.Equal(width, loaded.Width); Assert.Equal(height, loaded.Height);
            Assert.Equal(changed.Elements.Select(e => e.Bounds), loaded.Elements.Select(e => e.Bounds));
            Assert.Equal(changed.Elements.Select(e => e.FontSize), loaded.Elements.Select(e => e.FontSize));
            Assert.Equal(changed.EndPoses, loaded.EndPoses);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(360, 640, 120, 40)]
    [InlineData(1920, 1080, 1400, 800)]
    [InlineData(768, 1024, 760, 1000)]
    public void NewTextUsesTheWholeViewportAndStaysOnPaper(int width, int height, int x, int y)
    {
        var editor = new EditorSession();
        editor.History.Reset(new() { Width = width, Height = height });
        editor.AddText(new(x, y), "Text at the requested location");
        var text = Assert.Single(editor.Document.Elements);
        Assert.Equal(Math.Min(x, width - text.Bounds.Width), text.Bounds.X);
        Assert.InRange(text.Bounds.Bottom, 0, height);
    }

    [Fact]
    public void ChangingTheViewportIsOneReversibleEdit()
    {
        var editor = new EditorSession();
        var before = ExampleSketch.Create(); editor.History.Reset(before);
        editor.Commit(CanvasViewport.Resize(before, 1920, 1080));
        editor.History.Undo(); Assert.Equal(before, editor.Document);
        editor.History.Redo(); Assert.Equal(1920, editor.Document.Width);
        Assert.Same(editor.Document, CanvasViewport.Resize(editor.Document, 1920, 1080));
        Assert.Throws<ArgumentOutOfRangeException>(() => CanvasViewport.Resize(before, 0, 100000));
    }

    [Theory]
    [MemberData(nameof(Viewports))]
    public void PngUsesViewportDimensions(int width, int height)
    {
        using var image = Image.Load<Rgba32>(SketchExport.Png(CanvasViewport.Resize(ExampleSketch.Create(), width, height)));
        Assert.Equal(width * 2, image.Width); Assert.Equal(height * 2, image.Height);
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
    }

    [Theory]
    [InlineData(1920, 1080, 960, 540)]
    [InlineData(768, 1024, 720, 960)]
    public void LargeGifKeepsAspectRatioAndAnimationTiming(int width, int height, int outputWidth, int outputHeight)
    {
        var document = CanvasViewport.Resize(ExampleSketch.Create(), width, height) with { DurationMs = 300 };
        using var stream = new MemoryStream(); SketchExport.Gif(document, stream); stream.Position = 0;
        using var image = Image.Load<Rgba32>(stream);
        Assert.Equal(outputWidth, image.Width); Assert.Equal(outputHeight, image.Height);
        Assert.Equal(8, image.Frames.Count);
        Assert.Equal(160, Enumerable.Range(0, image.Frames.Count).Sum(i => image.Frames[i].Metadata.GetGifMetadata().FrameDelay));
    }

    [Theory]
    [InlineData(0, 640)]
    [InlineData(-360, 640)]
    [InlineData(100000, 100000)]
    public async Task LoadingRejectsUnsupportedDimensions(int width, int height)
    {
        var directory = Path.Combine(Path.GetTempPath(), "pen-sketch-tests", Guid.NewGuid().ToString());
        try
        {
            var path = Path.Combine(directory, "sketch.json"); Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new SketchDocument { Width = width, Height = height }));
            await Assert.ThrowsAsync<InvalidDataException>(() => new DocumentStore(path).LoadAsync());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(360, 640, 400, 700)]
    [InlineData(1920, 1080, 400, 700)]
    public void ZoomKeepsTheDocumentPointUnderThePinchCenter(int width, int height, int viewWidth, int viewHeight)
    {
        var fit = CanvasTransform.Fit(viewWidth, viewHeight, width, height);
        var anchor = fit.ToDocument(200, 300);
        var zoom = fit.Navigate(anchor, 240, 330, fit.Scale * 3);
        Assert.Equal(anchor.X, zoom.ToDocument(240, 330).X, 3);
        Assert.Equal(anchor.Y, zoom.ToDocument(240, 330).Y, 3);
        Assert.Equal(width / 2f, fit.ToDocument(viewWidth / 2f, viewHeight / 2f).X, 3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ResizeHandleRemainsVisibleAtDifferentDisplayScales(float scale)
    {
        var element = new SketchElement { Id = ElementId.New(), Kind = ElementKind.Rectangle, Bounds = new(20, 30, 100, 100) };
        var document = new SketchDocument { Elements = [element] };
        using var bitmap = new SkiaSharp.SKBitmap(360, 640);
        using var canvas = new SkiaSharp.SKCanvas(bitmap);
        SketchRenderer.Draw(canvas, document, grid: true, selected: element.Id, selectionScale: scale);
        var handle = bitmap.GetPixel((int)(120 + 8 * scale), 130);
        Assert.True(handle.Blue > handle.Red);
        Assert.NotEqual(SkiaSharp.SKColors.White, bitmap.GetPixel(16, 16));
        SketchRenderer.Draw(canvas, document, selected: ElementId.New());
        Assert.Equal(SkiaSharp.SKColors.White, bitmap.GetPixel(16, 16));
    }

    [Fact]
    public void TriggerCaptionStaysVisibleNearTheBottomOfALandscapeCanvas()
    {
        var document = new SketchDocument { Width = 640, Height = 360, Trigger = new() { X = 300, Y = 350, Label = "Open" } };
        using var image = Image.Load<Rgba32>(SketchExport.Png(document, showTrigger: true, scale: 1));
        Assert.True(image[300, 310].B > image[300, 310].R);
    }
}
