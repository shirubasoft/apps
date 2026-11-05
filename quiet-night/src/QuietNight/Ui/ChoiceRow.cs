using Microsoft.Maui.Layouts;

namespace QuietNight.Ui;

/// <summary>A row of pill buttons where exactly one is chosen.</summary>
public sealed class ChoiceRow : FlexLayout
{
    private readonly List<Button> buttons = [];
    private int selected;

    public ChoiceRow(IReadOnlyList<string> labels, int selected)
    {
        Wrap = FlexWrap.Wrap;
        JustifyContent = FlexJustify.Start;
        this.selected = selected;
        for (var i = 0; i < labels.Count; i++)
        {
            var index = i;
            var button = new Button
            {
                Text = labels[i], FontFamily = Theme.Semibold, FontSize = 14, CornerRadius = 18, HeightRequest = 40,
                Padding = new Thickness(14, 0), Margin = new Thickness(0, 0, 8, 8),
            };
            button.Clicked += (_, _) => Select(index);
            buttons.Add(button);
            Children.Add(button);
        }
        Paint();
    }

    public event Action<int>? Changed;

    public int Selected => selected;

    public void SetEnabled(bool enabled)
    {
        foreach (var button in buttons)
            button.IsEnabled = enabled;
    }

    private void Select(int index)
    {
        if (index == selected) return;
        selected = index;
        Paint();
        Changed?.Invoke(index);
    }

    private void Paint()
    {
        for (var i = 0; i < buttons.Count; i++)
        {
            buttons[i].BackgroundColor = i == selected ? Theme.Accent : Theme.Raised;
            buttons[i].TextColor = i == selected ? Theme.Background : Theme.Text;
        }
    }
}
