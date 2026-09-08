namespace PenSketch.Core.Documents;

public sealed class DocumentHistory
{
    private readonly List<SketchDocument> undo = [];
    private readonly Stack<SketchDocument> redo = new();
    public SketchDocument Current { get; private set; } = new();
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    public void Reset(SketchDocument document)
    {
        Current = document;
        undo.Clear();
        redo.Clear();
    }
    public void Commit(SketchDocument document)
    {
        if (Current == document) return;
        undo.Add(Current);
        if (undo.Count > 60) undo.RemoveAt(0);
        Current = document;
        redo.Clear();
    }
    public void Undo()
    {
        if (!CanUndo) return;
        redo.Push(Current);
        Current = undo[^1];
        undo.RemoveAt(undo.Count - 1);
    }
    public void Redo()
    {
        if (!redo.TryPop(out var document)) return;
        undo.Add(Current);
        Current = document;
    }
}
