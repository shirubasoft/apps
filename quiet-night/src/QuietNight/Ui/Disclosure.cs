using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace QuietNight.Ui;

/// <summary>A heading with a one-line summary that opens to show more, closed until the user asks.</summary>
public sealed class Disclosure : VerticalStackLayout
{
    private readonly Label title;
    private readonly Label summary = Theme.Body(size: 15);
    private readonly Path chevron = Theme.Icon(Icons.Expand, Theme.Haze, 22);
    private readonly Button toggle = new() { BackgroundColor = Colors.Transparent, CornerRadius = 16, Padding = 0 };
    private readonly View body;

    /// <param name="titleText">What the section holds.</param>
    /// <param name="body">What opening it shows.</param>
    /// <param name="small">Whether the title is a quiet link-like line rather than a heading.</param>
    public Disclosure(string titleText, View body, bool small = false)
    {
        this.body = body;
        Spacing = 12;
        title = small ? Theme.Strong(titleText, Theme.Lamplight, 16) : Theme.Heading(titleText);
        if (small) chevron.Fill = Theme.Lamplight;
        var header = new Grid
        {
            ColumnSpacing = 8,
            MinimumHeightRequest = Theme.Touch,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        var words = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, InputTransparent = true, Children = { title, summary } };
        // The toggle speaks for the header, so a screen reader meets one control rather than its parts.
        AutomationProperties.SetIsInAccessibleTree(words, false);
        AutomationProperties.SetIsInAccessibleTree(chevron, false);
        header.Add(words, 0);
        header.Add(chevron, 1);
        header.Add(toggle, 0);
        Grid.SetColumnSpan(toggle, 2);
        toggle.Clicked += async (_, _) =>
        {
            Open = !Open;
            // Opened, the section scrolls so its heading stays in view above what it reveals.
            if (Open && Scroller() is { } scroll) await scroll.ScrollToAsync(this, ScrollToPosition.MakeVisible, !Theme.ReducedMotion);
        };
        toggle.HandlerChanged += (_, _) => Describe();
        Children.Add(header);
        Children.Add(body);
        summary.IsVisible = false;
        Open = false;
    }

    public string Title
    {
        get => title.Text;
        set
        {
            title.Text = value;
            Describe();
        }
    }

    public string Summary
    {
        get => summary.Text;
        set
        {
            summary.Text = value;
            summary.IsVisible = value.Length > 0 && !body.IsVisible;
            Describe();
        }
    }

    public bool Open
    {
        get => body.IsVisible;
        set
        {
            body.IsVisible = value;
            chevron.Rotation = value ? 180 : 0;
            // Open, the body says it all; the summary would only repeat it.
            summary.IsVisible = !value && summary.Text.Length > 0;
            Describe();
        }
    }

    private ScrollView? Scroller()
    {
        Element? element = Parent;
        while (element is not null and not ScrollView)
            element = element.Parent;
        return element as ScrollView;
    }

    private void Describe()
    {
        SemanticProperties.SetDescription(toggle,
            $"{title.Text}{(summary.Text is { Length: > 0 } text ? $": {text.TrimEnd('.')}" : "")}. {(body.IsVisible ? "Expanded" : "Collapsed")}");
        // Android's own expanded and collapsed actions let screen readers say and change the state natively too.
        if (toggle.Handler?.PlatformView is Android.Views.View view)
            AndroidX.Core.View.ViewCompat.SetStateDescription(view, body.IsVisible ? "Expanded" : "Collapsed");
    }
}
