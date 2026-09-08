using PenSketch.Core.Drawing;
using PenSketch.Core.Animation;

namespace PenSketch.Core.Documents;

public static class ExampleSketch
{
    public static SketchDocument Create()
    {
        var card = new SketchElement { Id = ElementId.New(), Kind = ElementKind.Squircle, Bounds = new(30, 210, 300, 150) };
        var label = new SketchElement { Id = ElementId.New(), Kind = ElementKind.Text, Text = "Open details", Bounds = new(100, 260, 180, 40) };
        return new()
        {
            Name = "Example · expand card",
            Elements =
            [
                new() { Id = ElementId.New(), Kind = ElementKind.Text, Text = "My next screen", Bounds = new(30, 52, 300, 52) },
                new() { Id = ElementId.New(), Kind = ElementKind.Ellipse, Bounds = new(30, 125, 48, 48) },
                new() { Id = ElementId.New(), Kind = ElementKind.Rectangle, Bounds = new(96, 134, 220, 30) },
                card, label,
                new() { Id = ElementId.New(), Kind = ElementKind.Text, Text = "Tap the card to expand", Bounds = new(44, 430, 280, 36) }
            ],
            EndPoses = [ new() { Id = card.Id, Bounds = new(18, 190, 324, 330) }, new() { Id = label.Id, Bounds = new(100, 220, 180, 40), Opacity = 0.25f } ],
            Trigger = new() { X = 180, Y = 320, Kind = TriggerKind.Tap, Label = "Open details" }
        };
    }
}
