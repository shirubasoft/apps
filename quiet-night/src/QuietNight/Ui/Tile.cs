using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace QuietNight.Ui;

/// <summary>
/// A choice drawn as a tile: an optional icon over or beside its name, outlined in lamplight with a check mark when
/// chosen, so the choice never rests on colour alone. Screen readers hear its name and whether it is chosen.
/// </summary>
public sealed class Tile : Grid
{
    private readonly Border frame;
    private readonly Path? icon;
    private readonly Label label;
    private readonly Border badge;
    private readonly Path? inline;
    private readonly Button tap;
    private readonly string text;
    private readonly (string On, string Off) states;
    private readonly bool single;
    private readonly string? note;
    private bool selected;

    /// <param name="iconData">The icon's outline, or null for a tile with words only.</param>
    /// <param name="text">The tile's name.</param>
    /// <param name="stacked">Whether the icon sits above the name rather than beside it.</param>
    /// <param name="states">How a screen reader describes the tile when chosen and when not.</param>
    /// <param name="note">A second line under the name, or null for none.</param>
    /// <param name="single">Whether only one tile of its group can be chosen, so screen readers call it a radio button.</param>
    public Tile(string? iconData, string text, bool stacked, (string On, string Off) states, string? note = null, bool single = false)
    {
        this.single = single;
        this.note = note;
        this.text = text;
        this.states = states;
        label = new Label
        {
            Text = text, FontFamily = Theme.Semibold, FontSize = 16, LineBreakMode = LineBreakMode.WordWrap,
            HorizontalTextAlignment = stacked || (iconData is null && note is null) ? TextAlignment.Center : TextAlignment.Start,
            VerticalTextAlignment = TextAlignment.Center, InputTransparent = true,
        };
        View words = label;
        if (note is not null)
            words = new VerticalStackLayout { Spacing = 2, InputTransparent = true, Children = { label, Theme.Body(note, size: 15) } };
        View content;
        if (iconData is null)
        {
            // Words alone leave no room for a corner badge, so the check mark sits before them.
            inline = Theme.Icon(Icons.Check, Theme.Lamplight, 18);
            var row = new Grid { ColumnSpacing = 10, InputTransparent = true, HorizontalOptions = note is null ? LayoutOptions.Center : LayoutOptions.Fill };
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(18)));
            row.ColumnDefinitions.Add(new ColumnDefinition(note is null ? GridLength.Auto : GridLength.Star));
            row.Add(inline, 0);
            row.Add(words, 1);
            content = row;
        }
        else
        {
            icon = Theme.Icon(iconData, Theme.Haze, stacked ? 28 : 24);
            content = stacked
                ? new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, Children = { icon, label }, InputTransparent = true }
                : Beside(icon, words);
        }
        badge = new Border
        {
            BackgroundColor = Theme.Lamplight, StrokeThickness = 0, StrokeShape = new Ellipse(), WidthRequest = 20, HeightRequest = 20,
            HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 8, 8, 0),
            Content = Theme.Icon(Icons.Check, Theme.Ink, 14), InputTransparent = true,
        };
        frame = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 16 }, StrokeThickness = 1.5,
            Padding = stacked ? new Thickness(8, 12) : new Thickness(14, 10),
            MinimumHeightRequest = stacked ? 84 : 56,
            Content = content,
        };
        tap = new Button { BackgroundColor = Colors.Transparent, CornerRadius = 16, Padding = 0, BorderWidth = 0 };
        tap.Clicked += (_, _) => Tapped?.Invoke();
        tap.HandlerChanged += (_, _) => Announce();
        // The overlaid button speaks for the tile, so a screen reader meets one checkable control rather than its parts.
        AutomationProperties.SetIsInAccessibleTree(frame, false);
        AutomationProperties.SetIsInAccessibleTree(badge, false);
        Children.Add(frame);
        Children.Add(badge);
        Children.Add(tap);
        Paint();
    }

    public event Action? Tapped;

    /// <summary>The icon beside the words, which wrap rather than run past the tile's edge.</summary>
    private static Grid Beside(View icon, View words)
    {
        var row = new Grid
        {
            ColumnSpacing = 10, VerticalOptions = LayoutOptions.Center, InputTransparent = true,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
        };
        row.Add(icon, 0);
        row.Add(words, 1);
        return row;
    }

    public bool Selected
    {
        get => selected;
        set
        {
            selected = value;
            Paint();
        }
    }

    public void SetEnabled(bool enabled)
    {
        tap.IsEnabled = enabled;
        Opacity = enabled ? 1 : 0.5;
    }

    private void Paint()
    {
        frame.BackgroundColor = selected ? Theme.Slate : Colors.Transparent;
        // A chosen tile is filled and badged rather than outlined in lamplight, so a row of them stays calm.
        frame.Stroke = Theme.Outline;
        if (icon is not null) icon.Fill = selected ? Theme.Lamplight : Theme.Haze;
        label.TextColor = selected ? Theme.Moon : Theme.Haze;
        badge.IsVisible = selected && inline is null;
        if (inline is not null) inline.IsVisible = selected;
        SemanticProperties.SetDescription(tap, $"{text}, {(selected ? states.On : states.Off)}{(note is null ? "" : $". {note}")}");
        Announce();
    }

    /// <summary>Tells Android the tile is a check box or radio button and whether it is checked, as native controls do.</summary>
    private void Announce()
    {
        if (tap.Handler?.PlatformView is Android.Views.View view)
            AndroidX.Core.View.ViewCompat.SetAccessibilityDelegate(view, new Checkable(note is null ? text : $"{text}. {note}", selected, single));
    }

    private sealed class Checkable(string text, bool selected, bool single) : AndroidX.Core.View.AccessibilityDelegateCompat
    {
        public override void OnInitializeAccessibilityNodeInfo(Android.Views.View? host, AndroidX.Core.View.Accessibility.AccessibilityNodeInfoCompat? info)
        {
            base.OnInitializeAccessibilityNodeInfo(host, info);
            if (info is null) return;
            info.ClassName = single ? "android.widget.RadioButton" : "android.widget.CheckBox";
            info.Checkable = true;
            info.Checked = selected ? 1 : 0;
            info.ContentDescription = text;
        }
    }
}
