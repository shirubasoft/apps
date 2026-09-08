using PenSketch.Core.Drawing;
using SkiaSharp;

namespace PenSketch.Tests;

public class TextLayoutTests
{
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
