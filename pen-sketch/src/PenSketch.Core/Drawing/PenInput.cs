namespace PenSketch.Core.Drawing;

public enum PointerKind { Finger, Pen, Eraser, Mouse }
public sealed class PenInput
{
    private int? activePointer;
    public bool PenOnly { get; set; }
    public bool Begin(int id, PointerKind kind)
    {
        if (activePointer is not null || (PenOnly && kind == PointerKind.Finger)) return false;
        activePointer = id;
        return true;
    }
    public bool Owns(int id) => activePointer == id;
    public void End() => activePointer = null;
}
