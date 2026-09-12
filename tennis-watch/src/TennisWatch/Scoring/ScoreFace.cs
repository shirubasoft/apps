using System.Globalization;
using TennisWatch.Core.Matches;
using TennisWatch.Core.Scoring;

namespace TennisWatch.Scoring;

internal sealed class ScoreFace : AbsoluteLayout
{
    private readonly Label matchNumber = ScoreLabel(14);
    private readonly Label matchDate = ScoreLabel(11);
    private readonly Label leftPoints = PointLabel();
    private readonly Label rightPoints = PointLabel();
    private readonly Label separator = ScoreLabel(13);
    private readonly HorizontalStackLayout sets = new() { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
    private readonly BoxView line = new() { Color = Color.FromArgb("#343936") };
    private readonly ImageButton leftPlus;
    private readonly ImageButton rightPlus;
    private readonly ImageButton undo;
    private bool canUndo;

    public ScoreFace(Action<Side> award, Action undoPoint)
    {
        BackgroundColor = Colors.Black;
        matchNumber.FontFamily = "sans-serif-medium";
        matchNumber.AutomationId = "match-number";
        matchDate.AutomationId = "match-date";
        matchDate.TextColor = Color.FromArgb("#929B94");
        leftPoints.AutomationId = "left-points";
        rightPoints.AutomationId = "right-points";
        separator.Text = "×";
        separator.TextColor = Color.FromArgb("#8B938D");
        leftPlus = Control("plus.png", "Add point to left side", "left-plus", () => award(Side.Left));
        rightPlus = Control("plus.png", "Add point to right side", "right-plus", () => award(Side.Right));
        undo = Control("undo.png", "Undo last point", "undo", undoPoint);
        foreach (var view in new View[] { matchNumber, matchDate, leftPlus, leftPoints, separator, rightPoints, rightPlus, line, sets, undo })
            this.Add(view);
        SizeChanged += (_, _) => ArrangeFace();
    }

    public void Render(MatchSummary match)
    {
        matchNumber.Text = $"Match {match.Number:00}";
        matchDate.Text = match.StartedAt?.ToString("d MMM yyyy", CultureInfo.CurrentCulture) ?? "Date unavailable";
        SemanticProperties.SetDescription(matchNumber, $"Match {match.Number}");
        SemanticProperties.SetDescription(matchDate, match.StartedAt is { } started
            ? $"Match started {started:D}" : "Match date unavailable");
        leftPoints.Text = match.Score.PointText(Side.Left);
        rightPoints.Text = match.Score.PointText(Side.Right);
        SemanticProperties.SetDescription(leftPoints, $"Left points {leftPoints.Text}");
        SemanticProperties.SetDescription(rightPoints, $"Right points {rightPoints.Text}");
        sets.Clear();
        foreach (var score in match.Score.CompletedSets.TakeLast(2))
        {
            var label = ScoreLabel(12);
            label.Text = score.ToString();
            label.TextColor = Color.FromArgb("#929B94");
            SemanticProperties.SetDescription(label, $"Completed set, {score.Left} to {score.Right}");
            sets.Add(label);
        }
        var current = ScoreLabel(18);
        current.FontFamily = "sans-serif-medium";
        current.Text = match.Score.Games.ToString();
        current.AutomationId = "current-set";
        SemanticProperties.SetDescription(current, $"Current set games, {match.Score.Games.Left} to {match.Score.Games.Right}");
        sets.Add(current);
        canUndo = match.CanUndo;
        undo.IsEnabled = canUndo;
        ResetControls();
        ArrangeFace();
    }

    public void ResetControls()
    {
        leftPlus.Opacity = rightPlus.Opacity = 1;
        undo.Opacity = canUndo ? 0.8 : 0.3;
    }

    private ImageButton Control(string image, string description, string id, Action action)
    {
        var button = new ImageButton
        {
            Source = image, BackgroundColor = Colors.Transparent, Padding = 0,
            Aspect = Aspect.AspectFit, AutomationId = id, WidthRequest = 48, HeightRequest = 48
        };
        SemanticProperties.SetDescription(button, description);
        button.Clicked += (_, _) => action();
        button.Pressed += (_, _) => button.Opacity = 0.55;
        button.Released += (_, _) => ResetControls();
        return button;
    }

    private void ArrangeFace()
    {
        var size = Math.Min(Width, Height);
        if (size <= 0) return;
        var offsetX = (Width - size) / 2;
        var offsetY = (Height - size) / 2;
        void Place(View view, double x, double y, double width, double height) =>
            AbsoluteLayout.SetLayoutBounds(view, new Rect(offsetX + size * x - width / 2, offsetY + size * y - height / 2, width, height));

        Place(matchNumber, 0.5, 0.185, size * 0.65, 22);
        Place(matchDate, 0.5, 0.275, size * 0.7, 20);
        Place(leftPlus, 0.125, 0.43, 48, 48);
        Place(leftPoints, 0.332, 0.43, size * 0.25, 66);
        Place(separator, 0.5, 0.43, size * 0.075, 40);
        Place(rightPoints, 0.668, 0.43, size * 0.25, 66);
        Place(rightPlus, 0.875, 0.43, 48, 48);
        Place(line, 0.5, 0.575, size * 0.5, 0.75);
        Place(sets, 0.5, 0.655, size * 0.76, 32);
        Place(undo, 0.5, 0.825, 48, 48);
    }

    private static Label PointLabel()
    {
        var label = ScoreLabel(ScorePage.PointFontSize);
        label.FontFamily = "sans-serif-condensed";
        label.FontAttributes = FontAttributes.Bold;
        return label;
    }

    private static Label ScoreLabel(double size) => new()
    {
        FontFamily = "sans-serif", FontSize = size, TextColor = Color.FromArgb("#F4F6F2"),
        HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
        VerticalOptions = LayoutOptions.Center, MaxLines = 1,
        LineBreakMode = LineBreakMode.NoWrap, FontAutoScalingEnabled = true
    };
}
