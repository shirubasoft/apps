namespace NotifyClassifier.App.Pages;

internal static class PageStyles
{
    private static readonly Color Ink = Color.FromArgb("#18212B");
    private static readonly Color Muted = Color.FromArgb("#566473");

    public static Label Title(string text) => new()
    {
        Text = text,
        FontSize = 28,
        FontAttributes = FontAttributes.Bold,
        TextColor = Ink,
        Margin = new Thickness(0, 8, 0, 2)
    };

    public static Label Subtitle(string text) => new()
    {
        Text = text,
        FontSize = 14,
        TextColor = Muted,
        LineBreakMode = LineBreakMode.WordWrap
    };

    public static Button PrimaryButton(string text, string automationId) => new()
    {
        Text = text,
        AutomationId = automationId,
        BackgroundColor = Color.FromArgb("#2855D9"),
        TextColor = Colors.White,
        CornerRadius = 10,
        HeightRequest = 48
    };

    public static VerticalStackLayout Content() => new()
    {
        Padding = new Thickness(20, 12, 20, 32),
        Spacing = 14
    };

    public static BoxView Divider() => new()
    {
        HeightRequest = 1,
        Color = Color.FromArgb("#DCE2EA"),
        Margin = new Thickness(0, 4)
    };
}
