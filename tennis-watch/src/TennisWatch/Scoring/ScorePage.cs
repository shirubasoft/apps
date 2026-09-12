using TennisWatch.Core.Matches;
using TennisWatch.Core.Scoring;

namespace TennisWatch.Scoring;

public sealed class ScorePage : ContentPage
{
    private readonly MatchFile storage = new(Path.Combine(FileSystem.AppDataDirectory, "matches.json"));
    private readonly MatchBook book;
    private readonly AbsoluteLayout face = new() { BackgroundColor = Colors.Black };
    private readonly Label leftPoints = ScoreLabel(42);
    private readonly Label rightPoints = ScoreLabel(42);
    private readonly Label separator = ScoreLabel(14);
    private readonly HorizontalStackLayout sets = new() { Spacing = 12, HorizontalOptions = LayoutOptions.Center };
    private readonly BoxView line = new() { Color = Color.FromArgb("#424640") };
    private readonly ImageButton leftPlus;
    private readonly ImageButton rightPlus;
    private readonly ImageButton undo;

    public ScorePage()
    {
        BackgroundColor = Colors.Black;
        Padding = 0;
        SafeAreaEdges = SafeAreaEdges.None;
        NavigationPage.SetHasNavigationBar(this, false);
        book = storage.Load();
        leftPlus = Control("plus.png", "Add point to left side", "left-plus", () => book.Award(Side.Left));
        rightPlus = Control("plus.png", "Add point to right side", "right-plus", () => book.Award(Side.Right));
        undo = Control("undo.png", "Undo last point", "undo", book.Undo);
        leftPoints.AutomationId = "left-points";
        rightPoints.AutomationId = "right-points";
        separator.Text = "×";
        separator.TextColor = Color.FromArgb("#949B92");
        foreach (var view in new View[] { leftPlus, leftPoints, separator, rightPoints, rightPlus, line, sets, undo })
            face.Add(view);
        Content = face;
        face.SizeChanged += (_, _) => Arrange();
        Refresh();
    }

    public void Navigate(SwipeAction action)
    {
        switch (action)
        {
            case SwipeAction.NewMatch: Change(book.NewMatch); break;
            case SwipeAction.PreviousMatch: Change(book.PreviousMatch); break;
        }
    }

    private ImageButton Control(string image, string description, string id, Action action)
    {
        var button = new ImageButton
        {
            Source = image, BackgroundColor = Colors.Transparent, Padding = 0,
            Aspect = Aspect.AspectFit, AutomationId = id, WidthRequest = 48, HeightRequest = 48
        };
        SemanticProperties.SetDescription(button, description);
        button.Clicked += (_, _) => Change(action);
        button.Pressed += (_, _) => button.Opacity = 0.55;
        button.Released += (_, _) => button.Opacity = 1;
        return button;
    }

    private void Change(Action action)
    {
        action();
        storage.Save(book);
        Refresh();
    }

    private void Refresh()
    {
        leftPoints.Text = book.Score.PointText(Side.Left);
        rightPoints.Text = book.Score.PointText(Side.Right);
        SemanticProperties.SetDescription(leftPoints, $"Left points {leftPoints.Text}");
        SemanticProperties.SetDescription(rightPoints, $"Right points {rightPoints.Text}");
        sets.Clear();
        foreach (var score in book.Score.CompletedSets.TakeLast(2))
        {
            var label = ScoreLabel(12);
            label.Text = score.ToString();
            label.TextColor = Color.FromArgb("#949B92");
            SemanticProperties.SetDescription(label, $"Completed set, {score.Left} to {score.Right}");
            sets.Add(label);
        }
        var current = ScoreLabel(17);
        current.Text = book.Score.Games.ToString();
        current.AutomationId = "current-set";
        SemanticProperties.SetDescription(current, $"Current set games, {book.Score.Games.Left} to {book.Score.Games.Right}");
        sets.Add(current);
        undo.IsEnabled = book.CanUndo;
        undo.Opacity = book.CanUndo ? 0.75 : 0.3;
        Arrange();
    }

    private void Arrange()
    {
        var size = Math.Min(face.Width, face.Height);
        if (size <= 0) return;
        var offsetX = (face.Width - size) / 2;
        var offsetY = (face.Height - size) / 2;
        void Place(View view, double centerX, double centerY, double width, double height) =>
            AbsoluteLayout.SetLayoutBounds(view, new Rect(offsetX + size * centerX - width / 2,
                offsetY + size * centerY - height / 2, width, height));

        Place(leftPlus, 0.125, 0.48, 48, 48);
        Place(leftPoints, 0.33, 0.48, size * 0.23, 62);
        Place(separator, 0.5, 0.49, size * 0.1, 40);
        Place(rightPoints, 0.67, 0.48, size * 0.23, 62);
        Place(rightPlus, 0.875, 0.48, 48, 48);
        Place(line, 0.5, 0.615, size * 0.43, 0.75);
        Place(sets, 0.5, 0.685, size * 0.72, 32);
        Place(undo, 0.5, 0.855, 48, 48);
    }

    private static Label ScoreLabel(double fontSize) => new()
    {
        FontFamily = "sans-serif-light", FontSize = fontSize, TextColor = Color.FromArgb("#F3F5F0"),
        HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
        VerticalOptions = LayoutOptions.Center, MaxLines = 1,
        LineBreakMode = LineBreakMode.NoWrap, FontAutoScalingEnabled = true
    };
}
