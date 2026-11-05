using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace QuietNight.Ui;

/// <summary>
/// The night palette and the building blocks every page shares. Lamplight amber marks what can be done; moonlight blue
/// shows what the masker is doing. Both stay soft enough for a dark bedroom.
/// </summary>
public static class Theme
{
    public static readonly Color Ink = Color.FromArgb("#0B1020");
    public static readonly Color Dusk = Color.FromArgb("#151C30");
    public static readonly Color Slate = Color.FromArgb("#232C4A");
    public static readonly Color Moon = Color.FromArgb("#E8EBF4");
    public static readonly Color Haze = Color.FromArgb("#A3ABC6");
    public static readonly Color Lamplight = Color.FromArgb("#F2B766");
    public static readonly Color Moonlight = Color.FromArgb("#9FB0F0");
    public static readonly Color Ember = Color.FromArgb("#FF9C8A");

    /// <summary>Edges of controls that are not chosen, at least 3:1 against every surface.</summary>
    public static readonly Color Outline = Color.FromArgb("#6B779E");

    public const string Display = "FrauncesSoft";
    public const string Regular = "AtkinsonRegular";
    public const string Semibold = "AtkinsonSemibold";

    /// <summary>The smallest touch target, in device-independent pixels.</summary>
    public const double Touch = 48;

    /// <summary>Whether the user has turned animations off in Android's settings.</summary>
    public static bool ReducedMotion => Platform.AppContext.ContentResolver is { } resolver &&
        Android.Provider.Settings.Global.GetFloat(resolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;

    /// <summary>Whether TalkBack or another touch-exploring screen reader is on.</summary>
    public static bool ScreenReader =>
        Platform.AppContext.GetSystemService(Android.Content.Context.AccessibilityService) is Android.Views.Accessibility.AccessibilityManager { IsTouchExplorationEnabled: true };

    /// <summary>
    /// How long a message with an action stays up: the given time, or longer when the user has asked Android to give
    /// them more time to act.
    /// </summary>
    public static TimeSpan Timeout(TimeSpan wanted)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29) ||
            Platform.AppContext.GetSystemService(Android.Content.Context.AccessibilityService) is not Android.Views.Accessibility.AccessibilityManager manager)
            return wanted;
        var ms = manager.GetRecommendedTimeoutMillis((int)wanted.TotalMilliseconds,
            Android.Views.Accessibility.ContentMode.Controls | Android.Views.Accessibility.ContentMode.Text);
        return TimeSpan.FromMilliseconds(Math.Max(ms, wanted.TotalMilliseconds));
    }

    /// <summary>Whether the user has asked Android for large text, so side-by-side controls should stack instead.</summary>
    public static bool LargeText => (Platform.AppContext.Resources?.Configuration?.FontScale ?? 1) >= 1.15f;

    public static Label Title(string text) => Heading(new Label
    {
        Text = text, FontFamily = Display, FontSize = 32, TextColor = Moon, Margin = new Thickness(0, 12, 0, 0),
    }, SemanticHeadingLevel.Level1);

    /// <summary>The large words that say what the night is doing.</summary>
    public static Label State(string text = "") => Heading(new Label
    {
        Text = text, FontFamily = Display, FontSize = 34, TextColor = Moon, LineBreakMode = LineBreakMode.WordWrap,
    }, SemanticHeadingLevel.Level2);

    public static Label Heading(string text) => Heading(new Label
    {
        Text = text, FontFamily = Semibold, FontSize = 18, TextColor = Moon,
    }, SemanticHeadingLevel.Level2);

    public static Label Body(string text = "", Color? color = null, double size = 16) => new()
    {
        Text = text, FontFamily = Regular, FontSize = size, TextColor = color ?? Haze, LineBreakMode = LineBreakMode.WordWrap,
        LineHeight = 1.2,
    };

    public static Label Strong(string text = "", Color? color = null, double size = 16) => new()
    {
        Text = text, FontFamily = Semibold, FontSize = size, TextColor = color ?? Moon, LineBreakMode = LineBreakMode.WordWrap,
    };

    public static Border Card(params IView[] children) => Card(Dusk, children);

    public static Border Card(Color background, params IView[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 12 };
        foreach (var child in children)
            stack.Children.Add(child);
        return new Border
        {
            BackgroundColor = background,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 22 },
            Padding = new Thickness(18),
            Content = stack,
        };
    }

    /// <summary>The one action a screen is for.</summary>
    public static Button Primary(string text) => new()
    {
        Text = text, FontFamily = Semibold, FontSize = 18, TextColor = Ink, BackgroundColor = Lamplight,
        CornerRadius = 28, MinimumHeightRequest = 56, Padding = new Thickness(24, 12),
    };

    public static Button Secondary(string text) => new()
    {
        Text = text, FontFamily = Semibold, FontSize = 16, TextColor = Moon, BackgroundColor = Slate,
        CornerRadius = 24, MinimumHeightRequest = Touch, Padding = new Thickness(18, 10),
    };

    /// <summary>A quiet action that reads as a link.</summary>
    public static Button Quiet(string text, Color? color = null) => new()
    {
        Text = text, FontFamily = Semibold, FontSize = 16, TextColor = color ?? Lamplight, BackgroundColor = Colors.Transparent,
        MinimumHeightRequest = Touch, Padding = new Thickness(0, 10), HorizontalOptions = LayoutOptions.Start,
    };

    /// <summary>Greys a button out when it cannot be used, since Android's own disabled look barely shows on a dark page.</summary>
    public static void Enable(Button button, bool enabled, Color background, Color text)
    {
        button.IsEnabled = enabled;
        button.BackgroundColor = enabled ? background : Slate;
        button.TextColor = enabled ? text : Haze;
    }

    public static Slider Slider(double minimum, double maximum, double value, string description) => Describe(new Slider
    {
        Minimum = minimum, Maximum = maximum, Value = value, MinimumTrackColor = Lamplight, MaximumTrackColor = Outline,
        ThumbColor = Lamplight, MinimumHeightRequest = Touch,
    }, description);

    public static Path Icon(string data, Color color, double size = 24) => new()
    {
        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
        Fill = color,
        Aspect = Stretch.Uniform,
        WidthRequest = size,
        HeightRequest = size,
        VerticalOptions = LayoutOptions.Center,
        HorizontalOptions = LayoutOptions.Center,
        InputTransparent = true,
    };

    /// <summary>A round button showing only an icon, with its name for screen readers.</summary>
    public static Grid IconButton(string icon, string description, Color color, Action clicked, out Button button)
    {
        var tap = Describe(new Button
        {
            BackgroundColor = Slate, CornerRadius = 24, WidthRequest = Touch, HeightRequest = Touch, Padding = 0,
        }, description);
        tap.Clicked += (_, _) => clicked();
        button = tap;
        return new Grid { WidthRequest = Touch, HeightRequest = Touch, Children = { tap, Icon(icon, color, 22) } };
    }

    public static ScrollView Scroll(params IView[] children) => new() { Content = Stack(children) };

    public static VerticalStackLayout Stack(params IView[] children)
    {
        var stack = new VerticalStackLayout { Padding = new Thickness(20, 16, 20, 32), Spacing = 18 };
        foreach (var child in children)
            stack.Children.Add(child);
        return stack;
    }

    public static Grid Row(View left, View right, double spacing = 12)
    {
        var grid = new Grid
        {
            ColumnSpacing = spacing,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        grid.Add(left, 0);
        grid.Add(right, 1);
        left.VerticalOptions = LayoutOptions.Center;
        right.VerticalOptions = LayoutOptions.Center;
        return grid;
    }

    public static T Describe<T>(T view, string description, string? hint = null) where T : BindableObject
    {
        SemanticProperties.SetDescription(view, description);
        if (hint is not null) SemanticProperties.SetHint(view, hint);
        return view;
    }

    private static Label Heading(Label label, SemanticHeadingLevel level)
    {
        SemanticProperties.SetHeadingLevel(label, level);
        return label;
    }
}
