using PenSketch.Core.Drawing;
using PenSketch.Core.Documents;
using PenSketch.Core.Animation;

namespace PenSketch.Tests;

public class EditorTests
{
    [Theory]
    [InlineData(DrawingTool.Rectangle, ElementKind.Rectangle)]
        [InlineData(DrawingTool.Squircle, ElementKind.Squircle)]
    public void DragInEitherDirectionCreatesShape(DrawingTool tool, ElementKind kind)
    {
        var editor = new EditorSession { Tool = tool };
        editor.Begin(new(150, 200)); editor.Move(new(30, 50)); editor.End(new(30, 50));
        var shape = Assert.Single(editor.Document.Elements);
        Assert.Equal(kind, shape.Kind); Assert.Equal(new Bounds(30, 50, 120, 150), shape.Bounds);
        editor.History.Undo(); Assert.Empty(editor.Document.Elements);
        editor.History.Redo(); Assert.Single(editor.Document.Elements);
    }
    [Theory]
    [InlineData(DrawingTool.Square, ElementKind.Rectangle)]
    [InlineData(DrawingTool.Ellipse, ElementKind.Ellipse)]
    public void SquareAndCircleToolsStartWithEqualSides(DrawingTool tool, ElementKind kind)
    {
        var editor = new EditorSession { Tool = tool };
        editor.Begin(new(30, 50)); editor.End(new(150, 200));
        var shape = Assert.Single(editor.Document.Elements);
        Assert.Equal(kind, shape.Kind); Assert.Equal(new Bounds(30, 50, 120, 120), shape.Bounds);
    }
    [Theory]
    [InlineData(false, 80, 90)]
    [InlineData(true, 20, 30)]
    public void MovingEndStatePreservesStart(bool endState, int startX, int startY)
    {
        var editor = Box(); editor.EndState = endState;
        editor.Begin(new(40, 50)); editor.Move(new(100, 110)); editor.End(new(100, 110));
        Assert.Equal(startX, editor.Document.Elements[0].Bounds.X);
        Assert.Equal(startY, editor.Document.Elements[0].Bounds.Y);
        var atEnd = editor.Document.At(1).Single();
        Assert.Equal(new Bounds(80, 90, 100, 100), atEnd.Bounds);
    }
    [Theory]
    [InlineData(20, 30, 120, 130)]
    [InlineData(-200, -200, 16, 16)]
    public void CornerDragResizesWithMinimumSize(int dx, int dy, int width, int height)
    {
        var editor = Box(); editor.Selected = editor.Document.Elements[0].Id;
        editor.Begin(new(120, 130)); editor.End(new(120 + dx, 130 + dy));
        Assert.Equal(width, editor.Document.Elements[0].Bounds.Width);
        Assert.Equal(height, editor.Document.Elements[0].Bounds.Height);
    }
    [Theory]
    [InlineData(DrawingTool.Pen)]
    [InlineData(DrawingTool.Rectangle)]
    [InlineData(DrawingTool.Eraser)]
    [InlineData(DrawingTool.Select)]
    public void CancelRestoresPreGestureState(DrawingTool tool)
    {
        var editor = Box(); var before = editor.Document; editor.Tool = tool;
        editor.Begin(new(40, 50)); editor.Move(new(100, 130)); editor.Cancel();
        Assert.Equal(before, editor.Document);
    }
    [Theory]
    [InlineData(false, PointerKind.Finger, true)]
    [InlineData(true, PointerKind.Finger, false)]
    [InlineData(true, PointerKind.Pen, true)]
    [InlineData(true, PointerKind.Eraser, true)]
    [InlineData(true, PointerKind.Mouse, true)]
    public void PenOnlyRejectsFingersAndKeepsPointerOwnership(bool penOnly, PointerKind kind, bool expected)
    {
        var input = new PenInput { PenOnly = penOnly };
        Assert.Equal(expected, input.Begin(3, kind));
        if (expected) { Assert.False(input.Begin(4, PointerKind.Pen)); Assert.True(input.Owns(3)); }
        input.End(); Assert.True(input.Begin(4, PointerKind.Pen));
    }
    [Theory]
    [InlineData(0f, 20f, 1f)]
    [InlineData(0.5f, 60f, 0.6f)]
    [InlineData(1f, 100f, 0.2f)]
    [InlineData(2f, 100f, 0.2f)]
    public void TransitionInterpolatesBoundsAndOpacity(float progress, float x, float opacity)
    {
        var shape = Box().Document.Elements[0];
        var end = new ElementPose { Id = shape.Id, Bounds = new(100, 200, 240, 300), Opacity = 0.2f };
        var result = Transition.Interpolate(shape, end, progress);
        Assert.Equal(x, result.Bounds.X, 3); Assert.Equal(opacity, result.Opacity, 3);
    }
    [Theory]
    [InlineData(0, 0)]
    [InlineData(700, 0)]
    [InlineData(1200, 0.5)]
    [InlineData(1700, 1)]
    [InlineData(3000, 1)]
    public void TriggerIsShownBeforeMotion(double elapsed, float expected) => Assert.Equal(expected, Transition.Progress(elapsed, 1000));
    [Fact]
    public void InkCanMoveResizeAndRetainPressure()
    {
        var ink = InkStroke.Create(ElementId.New(), [new(10, 20, 0.2f), new(50, 100, 0.9f)]);
        Assert.Equal(new Bounds(10, 20, 40, 80), ink.Bounds);
        Assert.Equal(0.9f, ink.Points[1].Pressure);
        Assert.True(HitTest.Contains(ink, new(30, 60), 5));
        Assert.False(HitTest.Contains(ink, new(45, 25), 5));
        var moved = ink with { Bounds = ink.Bounds.Move(100, 0) };
        Assert.True(HitTest.Contains(moved, new(130, 60), 5));
    }
    [Fact]
    public void NewEditClearsRedoAndDeletingRemovesEndPose()
    {
        var editor = Box(); var element = editor.Document.Elements[0];
        editor.Commit(editor.Document.SetElement(element with { Opacity = 0 }, true));
        editor.Commit(editor.Document.Delete(element.Id));
        Assert.Empty(editor.Document.EndPoses);
        editor.History.Undo(); Assert.Single(editor.Document.Elements);
        editor.AddText(new(40, 80), "Label"); Assert.False(editor.History.CanRedo);
    }
    [Fact]
    public void TextAndTriggerAreCreatedAtRequestedPositions()
    {
        var editor = new EditorSession { Tool = DrawingTool.Text };
        InkPoint? requested = null; editor.TextRequested += p => requested = p;
        editor.Begin(new(50, 70)); editor.End(new(50, 70)); Assert.Equal(new InkPoint(50, 70), requested);
        editor.AddText(requested!.Value, "  Hello  "); Assert.Equal("Hello", editor.Document.Elements[0].Text);
        editor.Tool = DrawingTool.Trigger; editor.Begin(new(80, 100)); editor.End(new(80, 100));
        Assert.Equal(80, editor.Document.Trigger!.X); Assert.Equal(100, editor.Document.Trigger.Y);
    }
    private static EditorSession Box()
    {
        var editor = new EditorSession();
        editor.History.Reset(new() { Elements = [new() { Id = ElementId.New(), Kind = ElementKind.Rectangle, Bounds = new(20, 30, 100, 100) }] });
        return editor;
    }
}
