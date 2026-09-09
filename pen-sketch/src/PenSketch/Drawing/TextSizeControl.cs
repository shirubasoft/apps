using Android.Views;
using TextAlignment = Microsoft.Maui.TextAlignment;

namespace PenSketch.Drawing;

// Rotate the native slider inside a tall hit area, retaining Android's range accessibility.
public sealed class TextSizeControl : Grid
{
    public Slider Slider { get; } = new() { Minimum = 1, Maximum = 256, Value = 20, Rotation = -90 };
    private readonly Label caption = new() { Text = "Text size", FontSize = 12, HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label value = new() { Text = "20 px", FontSize = 12, HorizontalTextAlignment = TextAlignment.Center };

    public TextSizeControl()
    {
        WidthRequest = 60;
        RowDefinitions = new(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto));
        RowSpacing = 4;
        var track = new AbsoluteLayout { MinimumHeightRequest = 96, HorizontalOptions = LayoutOptions.Fill };
        track.Add(Slider);
        track.SizeChanged += (_, _) =>
        {
            var length = Math.Max(48, track.Height);
            AbsoluteLayout.SetLayoutBounds(Slider, new Rect((track.Width - length) / 2, (track.Height - 48) / 2, length, 48));
        };
        Add(caption); Add(track); Add(value);
        SetRow((IView)track, 1); SetRow((IView)value, 2);
        Slider.ValueChanged += (_, e) => value.Text = $"{e.NewValue:0} px";
        SemanticProperties.SetDescription(Slider, "Selected text size in canvas pixels. Slide up to enlarge.");
        Slider.HandlerChanged += (_, _) =>
        {
            if (Slider.Handler?.PlatformView is Android.Views.View native)
                native.KeyPress += (_, e) =>
                {
                    if (e.KeyCode is not (Keycode.DpadUp or Keycode.DpadDown)) return;
                    e.Handled = true;
                    if (e.Event?.Action == KeyEventActions.Down)
                        Slider.Value = Math.Clamp(Slider.Value + (e.KeyCode == Keycode.DpadUp ? 1 : -1), Slider.Minimum, Slider.Maximum);
                };
        };
    }

    public void SetSelection(float? fontSize)
    {
        Slider.IsEnabled = fontSize is not null;
        if (fontSize is { } size)
        {
            Slider.Maximum = Math.Max(256, Math.Ceiling(size));
            Slider.Value = size;
            value.Text = $"{size:0} px";
        }
        else value.Text = "Select\ntext";
    }
}
