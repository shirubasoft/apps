namespace QuietNight.Ui;

/// <summary>The night palette and the few building blocks every page shares.</summary>
public static class Theme
{
    public static readonly Color Background = Color.FromArgb("#0E1220");
    public static readonly Color Surface = Color.FromArgb("#171C2E");
    public static readonly Color Raised = Color.FromArgb("#222943");
    public static readonly Color Accent = Color.FromArgb("#9DB2FF");
    public static readonly Color AccentDeep = Color.FromArgb("#3C4C8F");
    public static readonly Color Text = Color.FromArgb("#E8EBF7");
    public static readonly Color Muted = Color.FromArgb("#8E95B3");
    public static readonly Color Warm = Color.FromArgb("#F2C47E");
    public static readonly Color Danger = Color.FromArgb("#FF8A80");

    public const string Regular = "OpenSansRegular";
    public const string Semibold = "OpenSansSemibold";

    public static Label Title(string text) => new()
    {
        Text = text, FontFamily = Semibold, FontSize = 26, TextColor = Text, Margin = new Thickness(0, 8, 0, 0),
    };

    public static Label Heading(string text) => new() { Text = text, FontFamily = Semibold, FontSize = 17, TextColor = Text };

    public static Label Body(string text = "", Color? color = null, double size = 14) => new()
    {
        Text = text, FontFamily = Regular, FontSize = size, TextColor = color ?? Muted, LineBreakMode = LineBreakMode.WordWrap,
    };

    public static Border Card(params IView[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        foreach (var child in children)
            stack.Children.Add(child);
        return new Border
        {
            BackgroundColor = Surface,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(18),
            Content = stack,
        };
    }

    public static Button Primary(string text) => new()
    {
        Text = text, FontFamily = Semibold, FontSize = 16, TextColor = Background, BackgroundColor = Accent,
        CornerRadius = 22, HeightRequest = 48, Padding = new Thickness(20, 0),
    };

    public static Button Secondary(string text) => new()
    {
        Text = text, FontFamily = Semibold, FontSize = 14, TextColor = Accent, BackgroundColor = Raised,
        CornerRadius = 20, HeightRequest = 44, Padding = new Thickness(16, 0),
    };

    public static Slider Slider(double minimum, double maximum, double value) => new()
    {
        Minimum = minimum, Maximum = maximum, Value = value, MinimumTrackColor = Accent, MaximumTrackColor = Raised, ThumbColor = Accent,
    };

    public static ScrollView Scroll(params IView[] children)
    {
        var stack = new VerticalStackLayout { Padding = new Thickness(20, 16, 20, 32), Spacing = 16 };
        foreach (var child in children)
            stack.Children.Add(child);
        return new ScrollView { Content = stack };
    }
}
