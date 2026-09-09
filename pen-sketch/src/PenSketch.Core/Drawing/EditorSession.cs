using PenSketch.Core.Animation;
using PenSketch.Core.Documents;

namespace PenSketch.Core.Drawing;

public sealed class EditorSession
{
    public DocumentHistory History { get; } = new();
    public SketchDocument Document => working ?? History.Current;
    public DrawingTool Tool { get; set; } = DrawingTool.Select;
    public bool EndState { get; set; }
    public ElementId? Selected { get; set; }
    public float HandleTolerance { get; set; } = 22;
    public event Action? Changed;
    public event Action<InkPoint>? TextRequested;
    private SketchDocument? working;
    private SketchElement? original;
    private InkPoint down;
    private readonly List<InkPoint> stroke = [];
    private ElementId gestureId;
    private bool resizing;
    private bool gestureActive;
    private DrawingTool gestureTool;

    public void Begin(InkPoint point, bool eraser = false)
    {
        Cancel();
        gestureActive = true;
        down = point;
        gestureTool = eraser ? DrawingTool.Eraser : Tool;
        working = History.Current;
        gestureId = ElementId.New();
        if (gestureTool == DrawingTool.Trigger)
        {
            working = working with { Trigger = (working.Trigger ?? new AnimationTrigger()) with { X = point.X, Y = point.Y } };
        }
        else if (gestureTool == DrawingTool.Select)
        {
            original = Document.At(EndState ? 1 : 0).FirstOrDefault(e => e.Id == Selected);
            resizing = original is not null && Math.Abs(point.X - original.Bounds.Right) <= HandleTolerance && Math.Abs(point.Y - original.Bounds.Bottom) <= HandleTolerance;
            if (!resizing) original = HitTest.Element(Document.At(EndState ? 1 : 0), point);
            Selected = original?.Id;
        }
        else if (gestureTool == DrawingTool.Eraser) Erase(point);
        else if (gestureTool == DrawingTool.Pen && !EndState)
        {
            stroke.Add(point);
            UpdateInk();
        }
        Changed?.Invoke();
    }
    public void Move(InkPoint point)
    {
        if (!gestureActive || working is null) return;
        if (gestureTool == DrawingTool.Select && original is not null)
        {
            var dx = point.X - down.X;
            var dy = point.Y - down.Y;
            var bounds = resizing ? original.Bounds.Resize(dx, dy) : original.Bounds.Move(dx, dy);
            bounds = bounds with
            {
                Width = Math.Min(bounds.Width, Document.Width), Height = Math.Min(bounds.Height, Document.Height)
            };
            bounds = bounds with { X = Math.Clamp(bounds.X, 0, Document.Width - bounds.Width), Y = Math.Clamp(bounds.Y, 0, Document.Height - bounds.Height) };
            working = working.SetElement(original with { Bounds = bounds }, EndState);
        }
        else if (gestureTool == DrawingTool.Eraser) Erase(point);
        else if (!EndState) CreateShape(point);
        Changed?.Invoke();
    }
    public void End(InkPoint point)
    {
        if (!gestureActive) return;
        Move(point);
        if (working is not null) History.Commit(working);
        working = null;
        gestureActive = false;
        stroke.Clear();
        if (gestureTool == DrawingTool.Text && !EndState) TextRequested?.Invoke(point);
        Changed?.Invoke();
    }
    public void Cancel()
    {
        working = null;
        gestureActive = false;
        original = null;
        stroke.Clear();
        Changed?.Invoke();
    }
    public void Commit(SketchDocument document)
    {
        Cancel();
        History.Commit(document);
        Changed?.Invoke();
    }
    public void Preview(SketchDocument document)
    {
        working = document;
        Changed?.Invoke();
    }
    public void CommitPreview()
    {
        if (working is not null) History.Commit(working);
        working = null;
        Changed?.Invoke();
    }
    public void AddText(InkPoint point, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var width = Math.Min(Document.Width, 220);
        var height = Math.Min(Document.Height, TextLayout.Height(text.Trim(), width));
        var element = new SketchElement
        {
            Id = ElementId.New(), Kind = ElementKind.Text, Text = text.Trim(),
            Bounds = new(Math.Clamp(point.X, 0, Document.Width - width), Math.Clamp(point.Y, 0, Document.Height - height), width, height)
        };
        Selected = element.Id;
        Commit(Document with { Elements = Document.Elements.Append(element).ToArray() });
        Tool = DrawingTool.Select;
    }
    private void Erase(InkPoint point)
    {
        var hit = HitTest.Element(Document.At(EndState ? 1 : 0), point);
        if (hit is not null && working is not null) working = working.Delete(hit.Id);
    }
    private void CreateShape(InkPoint point)
    {
        if (gestureTool == DrawingTool.Pen)
        {
            stroke.Add(point);
            UpdateInk();
            return;
        }
        var kind = gestureTool switch
        {
            DrawingTool.Rectangle or DrawingTool.Square => ElementKind.Rectangle,
            DrawingTool.Ellipse => ElementKind.Ellipse,
            DrawingTool.Squircle => ElementKind.Squircle,
            _ => (ElementKind?)null
        };
        if (kind is null) return;
        var bounds = Bounds.Between(down, point);
        if (gestureTool is DrawingTool.Square or DrawingTool.Ellipse)
        {
            var side = Math.Min(bounds.Width, bounds.Height);
            bounds = bounds with { Width = side, Height = side };
        }
        var element = new SketchElement { Id = gestureId, Kind = kind.Value, Bounds = bounds };
        working = History.Current with { Elements = History.Current.Elements.Append(element).ToArray() };
        Selected = element.Id;
    }
    private void UpdateInk()
    {
        var element = InkStroke.Create(gestureId, stroke);
        working = History.Current with { Elements = History.Current.Elements.Append(element).ToArray() };
        Selected = element.Id;
    }
}
