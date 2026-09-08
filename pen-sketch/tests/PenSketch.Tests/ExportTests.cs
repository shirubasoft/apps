using PenSketch.Core.Documents;
using PenSketch.Core.Export;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PenSketch.Tests;

public class ExportTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PngIsAnOpaqueCleanCanvasAtDoubleResolution(float progress)
    {
        using var image = Image.Load<Rgba32>(SketchExport.Png(ExampleSketch.Create(), progress));
        Assert.Equal(720, image.Width); Assert.Equal(1280, image.Height);
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.True(image[60, 295].R < 200);
    }
    [Fact]
    public void GifDecodesLoopsHoldsTriggerAndChangesBetweenStartAndEnd()
    {
        using var stream = new MemoryStream();
        SketchExport.Gif(ExampleSketch.Create(), stream);
        stream.Position = 0;
        using var image = Image.Load<Rgba32>(stream);
        Assert.Equal(360, image.Width); Assert.Equal(640, image.Height);
        Assert.Equal(22, image.Frames.Count);
        Assert.Equal(230, Enumerable.Range(0, image.Frames.Count).Sum(i => image.Frames[i].Metadata.GetGifMetadata().FrameDelay)); Assert.Equal(0, image.Metadata.GetGifMetadata().RepeatCount);
        Assert.Equal(70, image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay);
        Assert.Equal(60, image.Frames[^1].Metadata.GetGifMetadata().FrameDelay);
        Assert.NotEqual(image.Frames[0][30, 270], image.Frames[^1][30, 270]);
    }
    [Fact]
    public void GifRejectsMissingAnimationAndHonorsCancellation()
    {
        using var stream = new MemoryStream();
        Assert.Throws<InvalidOperationException>(() => SketchExport.Gif(new(), stream));
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.Throws<OperationCanceledException>(() => SketchExport.Gif(ExampleSketch.Create(), stream, source.Token));
    }
    [Fact]
    public async Task PersistenceRoundTripsInkAndEndPosesAndLeavesNoPartialFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "pen-sketch-tests", Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "sketch.json");
        try
        {
            var store = new DocumentStore(path);
            Assert.Empty((await store.LoadAsync()).Elements);
            var document = ExampleSketch.Create();
            await store.SaveAsync(document);
            var loaded = await store.LoadAsync();
            Assert.Equal(document.Name, loaded.Name);
            Assert.Equal(document.Elements.Select(e => e.Bounds), loaded.Elements.Select(e => e.Bounds));
            Assert.Equal(document.EndPoses, loaded.EndPoses);
            Assert.False(File.Exists(path + ".tmp"));
            await File.WriteAllTextAsync(path, "{\"Version\":99}");
            await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public void DescriptionIncludesTriggerAndChangedGeometry()
    {
        var text = SketchExport.Describe(ExampleSketch.Create());
        Assert.Contains("Trigger: Tap", text); Assert.Contains("Open details", text); Assert.Contains("height=330", text); Assert.Contains("opacity=0.25", text);
    }
}
