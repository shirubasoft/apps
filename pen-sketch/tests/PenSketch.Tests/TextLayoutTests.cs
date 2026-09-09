using PenSketch.Core.Drawing;
using SkiaSharp;

namespace PenSketch.Tests;

public class TextLayoutTests
{
    [Theory]
    [InlineData(12)]
    [InlineData(40)]
    [InlineData(96)]
    [InlineData(256)]
    public void ExplicitSizeGrowsTheBoxWithoutClippingTheText(float size)
    {
        var editor = new EditorSession(); editor.AddText(new(120, 580), "Hello world");
        var resized = TextLayout.Resize(editor.Document.Elements[0], size, 360, 640);
        var layout = TextLayout.Fit(resized.Text, resized.Bounds, resized.FontSize);
        Assert.Equal(resized.FontSize, layout.FontSize);
        Assert.Equal("Helloworld", string.Concat(layout.Lines).Replace(" ", ""));
        Assert.InRange(resized.Bounds.X, 0, 360 - resized.Bounds.Width);
        Assert.InRange(resized.Bounds.Y, 0, 640 - resized.Bounds.Height);
        if (size <= 96) Assert.Equal(size, resized.FontSize);
    }

    [Fact]
    public void FontSizePreviewAnimatesAndUndoesAsOneEdit()
    {
        var editor = new EditorSession(); editor.AddText(new(20, 30), "Headline");
        var before = editor.Document; var text = before.Elements[0];
        foreach (var size in new[] { 30, 42, 60 })
            editor.Preview(editor.Document.SetElement(TextLayout.Resize(text, size, 360, 640), true));
        editor.CommitPreview();
        Assert.Equal(20, editor.Document.At(0).Single().FontSize);
        Assert.Equal(40, editor.Document.At(0.5f).Single().FontSize);
        Assert.Equal(60, editor.Document.At(1).Single().FontSize);
        Assert.Contains("fontSize=60", PenSketch.Core.Export.SketchExport.Describe(editor.Document));
        editor.History.Undo(); Assert.Equal(before, editor.Document);
    }

    [Fact]
    public void LegacyDocumentsUseDefaultTextSizeAndInheritItInTheEndState()
    {
        const string json = """
            {"Elements":[{"Id":{"Value":"11111111-1111-1111-1111-111111111111"},"Kind":3,"Bounds":{"X":10,"Y":10,"Width":220,"Height":44},"Text":"Old sketch"}],
            "EndPoses":[{"Id":{"Value":"11111111-1111-1111-1111-111111111111"},"Bounds":{"X":40,"Y":50,"Width":240,"Height":60}}]}
            """;
        var document = System.Text.Json.JsonSerializer.Deserialize<PenSketch.Core.Documents.SketchDocument>(json)!;
        Assert.Equal(20, document.Elements[0].FontSize);
        Assert.Equal(20, document.At(1).Single().FontSize);
    }

    [Theory]
    [InlineData("W", 16, 200)]
    [InlineData("Wide letters WWWWWW", 16, 44)]
    public void ResizingAContainerFitsBothWidthAndHeight(string text, int width, int height)
    {
        var layout = TextLayout.Fit(text, new(0, 0, width, height), 96);
        using var font = new SKFont(SKTypeface.Default, layout.FontSize);
        Assert.All(layout.Lines, line => Assert.True(font.MeasureText(line) <= width));
        Assert.True(layout.Lines.Length * font.Spacing <= height);
    }
    [Theory]
    [InlineData("First line\nSecond line\nThird line", 220, 44)]
    [InlineData("averylongunbrokenidentifierthatmustwrapacrossmanylineswithoutlosingcharacters", 100, 80)]
    [InlineData("A label with several words that describes an interaction with the search field", 180, 44)]
    public void AcceptedTextFitsAndRetainsCharacters(string text, float width, float height)
    {
        var layout = TextLayout.Fit(text, new(0, 0, width, height));
        using var font = new SKFont(SKTypeface.Default, layout.FontSize);
        Assert.True(layout.Lines.Length * font.Spacing <= height);
        Assert.All(layout.Lines, line => Assert.True(font.MeasureText(line) <= width));
        Assert.Equal(text.Replace(" ", "").Replace("\n", ""), string.Concat(layout.Lines).Replace(" ", ""));
    }
    [Fact]
    public void EnlargingTheContainerDoesNotEnlargeTextAndHideLines()
    {
        const string text = "First line\nSecond line\nThird line";
        var layout = TextLayout.Fit(text, new(0, 0, 220, 150));
        Assert.Equal(TextLayout.DefaultFontSize, layout.FontSize);
        Assert.Equal(3, layout.Lines.Length);
    }
    [Fact]
    public void ContinuousOpacityPreviewCreatesOneUndoAction()
    {
        var editor = new EditorSession(); editor.AddText(new(10, 20), "Label");
        var before = editor.Document; var element = before.Elements[0];
        editor.Preview(before.SetElement(element with { Opacity = 0.7f }, true));
        editor.Preview(editor.Document.SetElement(element with { Opacity = 0.3f }, true));
        editor.CommitPreview();
        Assert.Equal(0.3f, editor.Document.At(1).Single().Opacity);
        editor.History.Undo(); Assert.Equal(before, editor.Document);
    }
}
