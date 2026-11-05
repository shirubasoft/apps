using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace QuietNight.Ui;

/// <summary>One thing the night needs, such as connected earbuds: a check mark when it is in place, or what to do.</summary>
public sealed class CheckRow : Grid
{
    private readonly Path icon;
    private readonly Label title = Theme.Strong(size: 16);
    private readonly Label detail = Theme.Body(size: 15);
    private readonly Path done = Theme.Icon(Icons.Check, Theme.Moonlight, 22);
    private readonly Button action = Theme.Secondary("");
    private readonly bool mark;
    private bool? ok;
    private string? shown;

    /// <param name="iconData">The icon for the need.</param>
    /// <param name="doneText">How the need reads in a list of what is in place, such as "earbuds connected".</param>
    /// <param name="mark">Whether a met need shows a check mark at the end of the row.</param>
    /// <param name="compact">Whether its button sits at the end of the row instead of under the words, for short steps.</param>
    public CheckRow(string iconData, string doneText, bool mark = true, bool compact = false)
    {
        Done = doneText;
        this.mark = mark;
        icon = Theme.Icon(iconData, Theme.Haze, 24);
        icon.VerticalOptions = LayoutOptions.Start;
        icon.Margin = new Thickness(0, 2, 0, 0);
        ColumnSpacing = 14;
        MinimumHeightRequest = Theme.Touch;
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(24)));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        action.TextColor = Theme.Lamplight;
        action.Clicked += (_, _) => Acted?.Invoke();
        done.VerticalOptions = LayoutOptions.Center;
        this.Add(icon, 0);
        var words = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { title, detail } };
        this.Add(words, 1);
        this.Add(done, 2);
        // With large text a button at the end would squeeze the words, so it goes under them instead.
        if (compact && !Theme.LargeText)
        {
            action.VerticalOptions = LayoutOptions.Center;
            this.Add(action, 2);
        }
        else
        {
            action.HorizontalOptions = LayoutOptions.Start;
            action.Margin = new Thickness(0, 6, 0, 0);
            words.Children.Add(action);
        }
    }

    public event Action? Acted;

    public bool Ok => ok == true;

    /// <summary>Whether the row's icon shows; a summary of what is in place can leave it out until everything is.</summary>
    public bool IconVisible
    {
        get => icon.IsVisible;
        set
        {
            icon.IsVisible = value;
            // Without its icon the row's words start at the edge, rather than leaving an empty column.
            ColumnDefinitions[0].Width = value ? new GridLength(24) : new GridLength(0);
            ColumnSpacing = value ? 14 : 0;
        }
    }

    public string Done { get; }

    /// <summary>What the row says now, such as "Run all night".</summary>
    public string Title => title.Text;

    /// <param name="ok">Whether the need is met.</param>
    /// <param name="titleText">What is true now, or what to do.</param>
    /// <param name="detailText">Why it matters; empty to leave out.</param>
    /// <param name="actionText">The button that fixes it, or null for none.</param>
    /// <param name="urgent">Whether it blocked something the user just tried, so it stands out.</param>
    public void Show(bool ok, string titleText, string detailText = "", string? actionText = null, bool urgent = false)
    {
        var key = $"{ok}|{titleText}|{detailText}|{actionText}|{urgent}";
        if (key == shown) return;
        shown = key;
        this.ok = ok;
        title.Text = titleText;
        title.TextColor = urgent ? Theme.Ember : Theme.Moon;
        detail.Text = detailText;
        detail.IsVisible = detailText.Length > 0;
        icon.Fill = ok ? Theme.Haze : urgent ? Theme.Ember : Theme.Lamplight;
        done.IsVisible = ok && mark;
        action.IsVisible = !ok && actionText is not null;
        action.Text = actionText ?? "";
        SemanticProperties.SetDescription(action, $"{actionText}: {titleText}");
    }
}
