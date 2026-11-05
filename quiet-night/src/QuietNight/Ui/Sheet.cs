using Microsoft.Maui.Controls.Shapes;

namespace QuietNight.Ui;

/// <summary>How an option in a <see cref="Sheet"/> is drawn.</summary>
public enum SheetStyle
{
    Plain,
    Primary,
    Danger,

    /// <summary>The likeliest answer: outlined in lamplight rather than filled, so it never reads as already chosen.</summary>
    Suggested,
}

/// <summary>One choice in a <see cref="Sheet"/>, with an optional icon and a state a screen reader adds, such as "suggested".</summary>
public sealed record SheetOption(string Text, string? Icon = null, SheetStyle Style = SheetStyle.Plain, string? State = null);

/// <summary>
/// A question that rises from the bottom of the page in the app's own dark colours, in place of Android's bright
/// dialogs, with large rows a sleepy thumb can hit. While it is open, screen readers see only the sheet within the page;
/// when it closes, focus returns to the control that opened it.
/// </summary>
public sealed class Sheet : Grid
{
    /// <summary>What <see cref="ShowAsync"/> returns when the user chose the cancel button itself.</summary>
    public const int Cancelled = -2;

    /// <summary>What <see cref="ShowAsync"/> returns when the sheet was dismissed with Back or a tap outside it.</summary>
    public const int Dismissed = -1;

    private readonly Label title = Theme.Heading("");
    private readonly Label message = Theme.Body();
    private readonly VerticalStackLayout options = new() { Spacing = 10 };
    private readonly Button cancel = Theme.Quiet("Cancel", Theme.Haze);
    private readonly Border card;
    private readonly ScrollView scroll;
    private TaskCompletionSource<int>? answer;
    private View[] behind = [];
    private View? from;

    public Sheet()
    {
        IsVisible = false;
        var backdrop = new BoxView { Color = Theme.Ink.WithAlpha(0.8f) };
        var dismiss = new TapGestureRecognizer();
        dismiss.Tapped += (_, _) => Close(Dismissed);
        backdrop.GestureRecognizers.Add(dismiss);
        // Cancel is the accessible way out; the backdrop is only a shortcut for a tap outside the sheet.
        AutomationProperties.SetIsInAccessibleTree(backdrop, false);
        title.FontSize = 22;
        // Cancel spans the sheet, so it is as easy to hit as any option.
        cancel.HorizontalOptions = LayoutOptions.Fill;
        cancel.MinimumHeightRequest = 52;
        cancel.Clicked += (_, _) => Close(Cancelled);
        var handle = new BoxView
        {
            WidthRequest = 36, HeightRequest = 4, CornerRadius = 2, Color = Theme.Haze, HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, -10, 0, 0),
        };
        AutomationProperties.SetIsInAccessibleTree(handle, false);
        var body = new VerticalStackLayout { Spacing = 14, Children = { handle, title, message, options, cancel } };
        // Large text can make the options taller than the screen, so they scroll inside the sheet.
        scroll = new ScrollView { Content = body };
        card = new Border
        {
            BackgroundColor = Theme.Dusk,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28, 28, 0, 0) },
            Padding = new Thickness(20, 24, 20, 12),
            VerticalOptions = LayoutOptions.End,
            Content = scroll,
        };
        Children.Add(backdrop);
        Children.Add(card);
    }

    /// <summary>
    /// Asks the question and returns the index of the chosen option, <see cref="Cancelled"/> for the cancel button, or
    /// <see cref="Dismissed"/> for Back or a tap outside.
    /// </summary>
    /// <param name="from">The control that opened the sheet, which gets focus back when it closes.</param>
    public Task<int> ShowAsync(View? from, string question, string? detail, IReadOnlyList<SheetOption> choices, string cancelText = "Cancel")
    {
        Close(Dismissed);
        this.from = from;
        // Everything else on the page is hidden from screen readers while the sheet is open.
        behind = Parent is Layout page ? [.. page.Children.OfType<View>().Where(v => v != this)] : [];
        title.Text = question;
        message.Text = detail ?? "";
        message.IsVisible = detail is not null;
        cancel.Text = cancelText;
        options.Children.Clear();
        for (var i = 0; i < choices.Count; i++)
            options.Children.Add(Option(choices[i], i));
        answer = new TaskCompletionSource<int>();
        foreach (var view in behind)
            AutomationProperties.SetIsInAccessibleTree(view, false);
        HideTabs(true);
        // A hidden sheet has no size of its own yet, so the page it covers gives the room it has.
        if (scroll.Content is View body && Parent is View { Height: > 0, Width: > 0 } area)
        {
            // The scrolling sheet is as tall as its options, up to most of the screen.
            var wanted = body.Measure(area.Width - card.Padding.HorizontalThickness, double.PositiveInfinity).Height;
            scroll.HeightRequest = Math.Min(wanted, area.Height * 0.85 - card.Padding.VerticalThickness);
            _ = scroll.ScrollToAsync(0, 0, false);
        }
        IsVisible = true;
        // Switching tabs would leave the question unanswered, so tabs wait until the sheet closes.
        if (Shell.Current is { } shell) shell.Navigating += HoldTabs;
        if (Theme.ReducedMotion)
        {
            card.TranslationY = 0;
            card.Opacity = 1;
        }
        else
        {
            card.TranslationY = 40;
            card.Opacity = 0;
            _ = card.TranslateToAsync(0, 0, 160, Easing.CubicOut);
            _ = card.FadeToAsync(1, 160);
        }
        title.SetSemanticFocus();
        return answer.Task;
    }

    /// <summary>Closes the sheet as if dismissed; returns whether it was open, for the back button.</summary>
    public bool Dismiss()
    {
        if (!IsVisible) return false;
        Close(Dismissed);
        return true;
    }

    private void Close(int choice)
    {
        var wasOpen = IsVisible;
        IsVisible = false;
        if (Shell.Current is { } shell) shell.Navigating -= HoldTabs;
        foreach (var view in behind)
            AutomationProperties.SetIsInAccessibleTree(view, true);
        if (wasOpen) HideTabs(false);
        behind = [];
        var pending = answer;
        answer = null;
        if (wasOpen) from?.SetSemanticFocus();
        from = null;
        pending?.TrySetResult(choice);
    }

    /// <summary>The tabs belong to the window, not the page, so screen readers are kept from them on Android's side.</summary>
    private static void HideTabs(bool hidden)
    {
        if (Platform.CurrentActivity?.Window?.DecorView is not { } root) return;
        if (Find<Google.Android.Material.BottomNavigation.BottomNavigationView>(root) is { } tabs)
            tabs.ImportantForAccessibility = hidden ? Android.Views.ImportantForAccessibility.NoHideDescendants : Android.Views.ImportantForAccessibility.Auto;
    }

    private static T? Find<T>(Android.Views.View view) where T : Android.Views.View
    {
        if (view is T found) return found;
        if (view is not Android.Views.ViewGroup group) return null;
        for (var i = 0; i < group.ChildCount; i++)
            if (group.GetChildAt(i) is { } child && Find<T>(child) is { } inner)
                return inner;
        return null;
    }

    private void HoldTabs(object? sender, ShellNavigatingEventArgs e)
    {
        if (IsVisible && e.CanCancel) e.Cancel();
    }

    private Grid Option(SheetOption option, int index)
    {
        var (background, text) = option.Style switch
        {
            SheetStyle.Primary => (Theme.Lamplight, Theme.Ink),
            SheetStyle.Danger => (Theme.Slate, Theme.Ember),
            SheetStyle.Suggested => (Theme.Slate, Theme.Lamplight),
            _ => (Theme.Slate, Theme.Moon),
        };
        var label = new Label
        {
            Text = option.Text, FontFamily = Theme.Semibold, FontSize = 17, TextColor = text,
            VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap,
        };
        // The label wraps within the row; with large text the state moves under it rather than squeezing it.
        var line = new Grid
        {
            ColumnSpacing = 14, RowSpacing = 2, InputTransparent = true, VerticalOptions = LayoutOptions.Center,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        if (option.Icon is not null) line.Add(Theme.Icon(option.Icon, option.Style == SheetStyle.Plain ? Theme.Moonlight : text, 24), 0, 0);
        line.Add(label, 1, 0);
        if (option.State == "Current")
        {
            // The current answer carries a check mark; screen readers hear "Current" from the button's description.
            line.Add(Theme.Icon(Icons.Check, text, 22), 2, 0);
        }
        else if (option.State is not null)
        {
            // The state is written as well as coloured, so a suggestion never reads as an answer already given.
            var tag = new Label { Text = option.State, FontFamily = Theme.Semibold, FontSize = 15, TextColor = text, VerticalTextAlignment = TextAlignment.Center };
            if (Theme.LargeText)
            {
                line.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                line.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                line.Add(tag, 1, 1);
            }
            else line.Add(tag, 2, 0);
        }
        var face = new Border
        {
            BackgroundColor = background, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(18, 12), MinimumHeightRequest = 56, Content = line, InputTransparent = true,
        };
        AutomationProperties.SetIsInAccessibleTree(face, false);
        if (option.Style == SheetStyle.Suggested)
        {
            face.Stroke = Theme.Lamplight;
            face.StrokeThickness = 2;
        }
        var tap = Theme.Describe(new Button { BackgroundColor = Colors.Transparent, CornerRadius = 18, Padding = 0 },
            option.State is null ? option.Text : $"{option.Text}, {option.State}");
        tap.Clicked += (_, _) => Close(index);
        return new Grid { Children = { face, tap } };
    }
}
