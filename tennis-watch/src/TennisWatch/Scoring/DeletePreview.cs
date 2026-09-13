using Microsoft.Maui.Controls.Shapes;

namespace TennisWatch.Scoring;

internal sealed class DeletePreview : AbsoluteLayout
{
    private readonly Ellipse background = new() { Fill = new SolidColorBrush(Color.FromArgb("#FF6B6B")), StrokeThickness = 0 };
    private readonly Image trash = new() { Source = "trash.png", Aspect = Aspect.AspectFit, AutomationId = "delete-preview" };
    private double progress;

    public DeletePreview()
    {
        InputTransparent = true;
        IsVisible = false;
        this.Add(background);
        this.Add(trash);
        SemanticProperties.SetDescription(trash, "Hold to delete match");
        SizeChanged += (_, _) => ArrangePreview();
    }

    public double HiddenOffset => Height / 2;

    public void Reveal(double value)
    {
        progress = Math.Clamp(value, 0, 1);
        IsVisible = progress > 0;
        TranslationY = HiddenOffset * (1 - progress);
    }

    private void ArrangePreview()
    {
        var size = Math.Min(Width, Height);
        if (size <= 0) return;
        AbsoluteLayout.SetLayoutBounds(background, new Rect((Width - size) / 2, Height / 2, size, size));
        AbsoluteLayout.SetLayoutBounds(trash, new Rect((Width - 48) / 2, Height / 2 + size * 0.27 - 24, 48, 48));
        TranslationY = HiddenOffset * (1 - progress);
    }
}
