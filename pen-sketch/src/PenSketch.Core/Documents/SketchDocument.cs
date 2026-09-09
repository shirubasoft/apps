using PenSketch.Core.Animation;
using PenSketch.Core.Drawing;

namespace PenSketch.Core.Documents;

public sealed record SketchDocument
{
    public int Version { get; init; } = 1;
    public string Name { get; init; } = "Untitled sketch";
    public int Width { get; init; } = 360;
    public int Height { get; init; } = 640;
    public SketchElement[] Elements { get; init; } = [];
    public ElementPose[] EndPoses { get; init; } = [];
    public AnimationTrigger? Trigger { get; init; }
    public int DurationMs { get; init; } = 1000;

    public IEnumerable<SketchElement> At(float progress) => Elements.Select(element =>
        Transition.Interpolate(element, EndPoses.FirstOrDefault(pose => pose.Id == element.Id), progress));

    public SketchDocument SetElement(SketchElement element, bool endState)
    {
        if (!endState) return this with { Elements = Elements.Select(e => e.Id == element.Id ? element : e).ToArray() };
        var pose = new ElementPose { Id = element.Id, Bounds = element.Bounds, Opacity = element.Opacity, FontSize = element.FontSize };
        return this with { EndPoses = EndPoses.Where(p => p.Id != element.Id).Append(pose).ToArray() };
    }

    public SketchDocument Delete(ElementId id) => this with
    {
        Elements = Elements.Where(e => e.Id != id).ToArray(),
        EndPoses = EndPoses.Where(p => p.Id != id).ToArray()
    };
}
