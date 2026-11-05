using Microsoft.Maui.Layouts;

namespace QuietNight.Ui;

/// <summary>A row of pill buttons that switch on and off independently; at least one always stays on.</summary>
public sealed class ToggleRow : FlexLayout
{
    private readonly List<Button> buttons = [];
    private readonly bool[] on;

    public ToggleRow(IReadOnlyList<string> labels, IReadOnlyList<bool> on)
    {
        Wrap = FlexWrap.Wrap;
        JustifyContent = FlexJustify.Start;
        this.on = [.. on];
        for (var i = 0; i < labels.Count; i++)
        {
            var index = i;
            var button = new Button
            {
                Text = labels[i], FontFamily = Theme.Semibold, FontSize = 14, CornerRadius = 18, HeightRequest = 40,
                Padding = new Thickness(14, 0), Margin = new Thickness(0, 0, 8, 8), AutomationId = $"toggle-{labels[i].ToLowerInvariant()}",
            };
            button.Clicked += (_, _) => Toggle(index);
            buttons.Add(button);
            Children.Add(button);
        }
        Paint();
    }

    /// <summary>Raised with the index of the button and whether it is now on.</summary>
    public event Action<int, bool>? Changed;

    private void Toggle(int index)
    {
        if (on[index] && on.Count(o => o) == 1) return;
        on[index] = !on[index];
        Paint();
        Changed?.Invoke(index, on[index]);
    }

    private void Paint()
    {
        for (var i = 0; i < buttons.Count; i++)
        {
            buttons[i].BackgroundColor = on[i] ? Theme.Accent : Theme.Raised;
            buttons[i].TextColor = on[i] ? Theme.Background : Theme.Text;
        }
    }
}
