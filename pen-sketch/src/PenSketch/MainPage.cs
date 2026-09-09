using Stopwatch = System.Diagnostics.Stopwatch;
using PenSketch.Core.Animation;
using PenSketch.Core.Documents;
using PenSketch.Core.Drawing;
using PenSketch.Core.Export;
using PenSketch.Drawing;

namespace PenSketch;

// THESIS: A pen notebook whose drawings become an explicit start-to-end motion sketch.
// OWN-WORLD: White drawing paper, blue selection and actions, slate Material controls, system type.
// STORY: Draw the interface, set its end state, mark the trigger, share an image.
// FIRST VIEWPORT: App bar and two modes above a large portrait canvas; tools stay within thumb reach.
// FORM: Native editor, direct manipulation. Progress scrubbing is the signature interaction.
// FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
public sealed class MainPage : ContentPage
{
    private readonly EditorSession session = new();
    private readonly DocumentStore store = new(Path.Combine(FileSystem.AppDataDirectory, "current-sketch.json"));
    private readonly PenCanvas canvas;
    private readonly Label title = new() { FontSize = 22, FontAttributes = FontAttributes.Bold, Text = "Pen Sketch", VerticalTextAlignment = TextAlignment.Center };
    private readonly Label status = new() { FontSize = 12, Text = "Saved on this phone", VerticalTextAlignment = TextAlignment.Center };
    private readonly Label hint = new() { FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button drawMode;
    private readonly Button animateMode;
    private readonly Button undo;
    private readonly Button redo;
    private readonly Button start;
    private readonly Button end;
    private readonly Button play;
    private readonly Button trigger;
    private readonly Button export;
    private readonly Button viewport;
    private readonly TextSizeControl textSize = new();
    private SketchElement? textBeforeResize;
    private bool textSizeDragging;
    private readonly HorizontalStackLayout drawTools = new() { Spacing = 8 };
    private readonly VerticalStackLayout animationTools = new() { Spacing = 4 };
    private readonly HorizontalStackLayout selectionTools = new() { Spacing = 8 };
    private readonly Dictionary<DrawingTool, Button> tools = [];
    private readonly Slider opacity = new() { Minimum = 0, Maximum = 1, Value = 1, MinimumWidthRequest = 100, HorizontalOptions = LayoutOptions.Fill };
    private readonly Label opacityLabel = new() { Text = "Opacity", FontSize = 12, VerticalTextAlignment = TextAlignment.Center };
    private readonly Grid opacityRow;
    private readonly Label progressLabel = new() { Text = "Preview 0%", FontSize = 12 };
    private bool opacityDragging;
    private readonly Slider scrubber = new() { Minimum = 0, Maximum = 1, Value = 0 };
    private readonly Picker duration = new() { Title = "Duration", WidthRequest = 100, FontSize = 14 };
    private readonly ScrollView drawScroll;
    private bool animate;
    private bool updating;
    private bool loaded;
    private bool busy;
    private bool saveBlocked;
    private CancellationTokenSource? playback;

    private static Color Surface => Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#161D28") : Color.FromArgb("#F1F4F8");
    private static Color Foreground => Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#E7EDF7") : Color.FromArgb("#252A31");
    private static Color Muted => Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#BDC8DA") : Color.FromArgb("#526174");
    private static Color Primary => Color.FromArgb("#246BCE");
    private static Color Tonal => Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#29374C") : Color.FromArgb("#DFE8F5");

    public MainPage()
    {
        Title = "Pen Sketch";
        SafeAreaEdges = SafeAreaEdges.All;
        canvas = new(session);
        export = ActionButton("Export", ExportAsync, true);
        var menu = ActionButton("Sketch", SketchMenuAsync);
        var header = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto, GridLength.Auto), ColumnSpacing = 8 };
        header.Add(title); header.Add(menu, 1); header.Add(export, 2);
        drawMode = ActionButton("Draw", () => SetMode(false));
        animateMode = ActionButton("Animate", () => SetMode(true));
        var modes = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Star), ColumnSpacing = 8 };
        modes.Add(drawMode); modes.Add(animateMode, 1);
        viewport = ActionButton("360 × 640", ViewportAsync);
        var viewportRow = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto, GridLength.Auto), ColumnSpacing = 8 };
        viewportRow.Add(viewport);
        viewportRow.Add(ActionButton("Rotate", () => ChangeViewport(session.Document.Height, session.Document.Width)), 1);
        viewportRow.Add(ActionButton("Fit", () => canvas.ResetView()), 2);
        foreach (var (tool, name) in new[] { (DrawingTool.Select, "Select"), (DrawingTool.Pen, "Pen"), (DrawingTool.Rectangle, "Box"), (DrawingTool.Square, "Square"), (DrawingTool.Ellipse, "Circle"), (DrawingTool.Squircle, "Squircle"), (DrawingTool.Text, "Text"), (DrawingTool.Eraser, "Erase") })
        {
            var button = ActionButton(name, () => { StopPlayback(); session.Tool = tool; Refresh(); });
            tools[tool] = button;
            drawTools.Add(button);
        }
        drawScroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = drawTools, HorizontalScrollBarVisibility = ScrollBarVisibility.Never };
        var penSwitch = new Switch { OnColor = Primary, IsToggled = Preferences.Get("pen-only", false) };
        canvas.PenOnly = penSwitch.IsToggled;
        penSwitch.Toggled += (_, e) => { canvas.PenOnly = e.Value; Preferences.Set("pen-only", e.Value); Refresh(); };
        var penLabel = new Label { Text = "Pen only", FontSize = 12, VerticalTextAlignment = TextAlignment.Center };
        SemanticProperties.SetDescription(penSwitch, "Pen only. Ignore fingers on the drawing canvas.");
        var stateRow = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto, GridLength.Auto), ColumnSpacing = 8 };
        stateRow.Add(status); stateRow.Add(penLabel, 1); stateRow.Add(penSwitch, 2);
        undo = ActionButton("Undo", () => { StopPlayback(); session.Cancel(); session.History.Undo(); Refresh(); Save(); });
        redo = ActionButton("Redo", () => { StopPlayback(); session.Cancel(); session.History.Redo(); Refresh(); Save(); });
        var delete = ActionButton("Delete", () => { if (session.Selected is { } id) { session.Commit(session.Document.Delete(id)); session.Selected = null; Refresh(); Save(); } });
        var edit = ActionButton("Edit text", EditSelectedTextAsync);
        var duplicate = ActionButton("Copy", () =>
        {
            var selected = SelectedElement();
            if (selected is null || animate) return;
            var copy = selected with { Id = ElementId.New(), Bounds = selected.Bounds.Move(12, 12) };
            session.Commit(session.Document with { Elements = session.Document.Elements.Append(copy).ToArray() });
            session.Selected = copy.Id; Refresh(); Save();
        });
        selectionTools.Add(undo); selectionTools.Add(redo); selectionTools.Add(edit); selectionTools.Add(duplicate); selectionTools.Add(delete);
        start = ActionButton("Start", () => SelectState(false));
        end = ActionButton("End", () => SelectState(true));
        play = ActionButton("Play", PlayAsync, true);
        trigger = ActionButton("Set trigger", TriggerAsync);
        foreach (var label in new[] { "0.3 s", "0.6 s", "1.0 s", "1.5 s", "2.0 s", "3.0 s" }) duration.Items.Add(label);
        duration.SelectedIndex = 2;
        duration.SelectedIndexChanged += (_, _) =>
        {
            if (updating || duration.SelectedIndex < 0) return;
            StopPlayback();
            session.Commit(session.Document with { DurationMs = new[] { 300, 600, 1000, 1500, 2000, 3000 }[duration.SelectedIndex] }); Save();
        };
        var frames = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Star, GridLength.Auto), ColumnSpacing = 8 };
        frames.Add(start); frames.Add(end, 1); frames.Add(play, 2);
        var triggerRow = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Auto, GridLength.Star), ColumnSpacing = 8 };
        triggerRow.Add(trigger); triggerRow.Add(duration, 1);
        triggerRow.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { progressLabel, scrubber } }, 2);
        scrubber.ValueChanged += (_, e) =>
        {
            progressLabel.Text = $"Preview {e.NewValue:P0}";
            if (updating || playback is not null) return;
            StopPlayback(false); session.Cancel(); canvas.IsPlaying = true;
            canvas.Progress = (float)e.NewValue; Refresh();
            hint.Text = "Preview · choose Start or End to continue editing.";
        };
        SemanticProperties.SetDescription(scrubber, "Preview transition progress");
        animationTools.Add(frames); animationTools.Add(triggerRow);
        opacityRow = new Grid { ColumnDefinitions = Columns(GridLength.Auto, GridLength.Star), ColumnSpacing = 8 };
        opacityRow.Add(opacityLabel); opacityRow.Add(opacity, 1);
        opacity.DragStarted += (_, _) => { session.Cancel(); opacityDragging = true; };
        opacity.DragCompleted += (_, _) =>
        {
            opacityDragging = false; session.CommitPreview(); Refresh(); Save();
        };
        opacity.ValueChanged += (_, e) =>
        {
            opacityLabel.Text = $"Opacity {e.NewValue:P0}";
            if (updating || SelectedElement() is not { } element) return;
            updating = true;
            var document = session.Document.SetElement(element with { Opacity = (float)e.NewValue }, session.EndState);
            if (opacityDragging) session.Preview(document); else session.Commit(document);
            updating = false;
            if (!opacityDragging) { Refresh(); Save(); }
        };
        SemanticProperties.SetDescription(opacity, "Selected object opacity");
        textSize.Slider.DragStarted += (_, _) =>
        {
            session.Cancel(); textBeforeResize = SelectedElement(); textSizeDragging = true;
        };
        textSize.Slider.DragCompleted += (_, _) =>
        {
            textSizeDragging = false; textBeforeResize = null; session.CommitPreview(); Refresh(); Save();
        };
        textSize.Slider.ValueChanged += (_, e) =>
        {
            if (updating || canvas.IsPlaying || (textBeforeResize ?? SelectedElement()) is not { Kind: ElementKind.Text } element) return;
            var resized = TextLayout.Resize(element, (float)Math.Round(e.NewValue), session.Document.Width, session.Document.Height);
            var document = session.Document.SetElement(resized, session.EndState);
            if (textSizeDragging) session.Preview(document);
            else { session.Commit(document); Refresh(); Save(); }
        };
        var controls = new VerticalStackLayout { Spacing = 4, Children = { drawScroll, animationTools } };
        var top = new VerticalStackLayout { Spacing = 6, Children = { header, modes, viewportRow } };
        var bottom = new VerticalStackLayout { Spacing = 4, Children =
        {
            opacityRow, stateRow, new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = selectionTools, HorizontalScrollBarVisibility = ScrollBarVisibility.Never }, hint
        } };
        var root = new Grid { Padding = new Thickness(12, 6, 12, 8), RowDefinitions = Rows(GridLength.Auto, GridLength.Auto, GridLength.Star, GridLength.Auto), RowSpacing = 8 };
        var workspace = new Grid { ColumnDefinitions = Columns(GridLength.Star, GridLength.Auto), ColumnSpacing = 8 };
        workspace.Add(canvas); workspace.Add(textSize, 1);
        root.Add(top); root.Add(controls, 0, 1); root.Add(workspace, 0, 2); root.Add(bottom, 0, 3);
        var sidePanel = new VerticalStackLayout { Spacing = 8 };
        var sideScroll = new ScrollView { Content = sidePanel };
        var wide = false;
        root.SizeChanged += (_, _) =>
        {
            var nextWide = root.Width >= 640;
            if (nextWide == wide) return;
            wide = nextWide;
            sidePanel.Children.Clear(); root.Children.Clear();
            if (wide)
            {
                root.RowDefinitions = Rows(GridLength.Star);
                root.ColumnDefinitions = Columns(new GridLength(320), GridLength.Star);
                root.ColumnSpacing = 12;
                sidePanel.Add(top); sidePanel.Add(controls); sidePanel.Add(bottom);
                root.Add(sideScroll); root.Add(workspace, 1);
            }
            else
            {
                root.RowDefinitions = Rows(GridLength.Auto, GridLength.Auto, GridLength.Star, GridLength.Auto);
                root.ColumnDefinitions = Columns(GridLength.Star);
                root.Add(top); root.Add(controls, 0, 1); root.Add(workspace, 0, 2); root.Add(bottom, 0, 3);
            }
        };
        Content = root;
        session.Changed += () => { if (!updating) RefreshSelection(); };
        session.TextRequested += point => MainThread.BeginInvokeOnMainThread(async () => await EnterTextAsync(point));
        canvas.GestureCompleted += () => { Refresh(); Save(); };
        Application.Current!.RequestedThemeChanged += (_, _) => ApplyTheme(root);
        ApplyTheme(root);
        SetMode(false);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (loaded) return;
        loaded = true;
        try { session.History.Reset(await store.LoadAsync()); status.Text = "Saved on this phone"; }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { status.Text = "Saved sketch could not be opened"; await DisplayAlertAsync("Could not open sketch", error.Message + " Your saved file has been kept.", "OK"); saveBlocked = true; }
        Refresh();
    }
    protected override void OnDisappearing() { StopPlayback(); base.OnDisappearing(); }
    private static ColumnDefinitionCollection Columns(params GridLength[] widths) => new(widths.Select(w => new ColumnDefinition(w)).ToArray());
    private static RowDefinitionCollection Rows(params GridLength[] heights) => new(heights.Select(h => new RowDefinition(h)).ToArray());
    private Button ActionButton(string text, Action action, bool primary = false) => ActionButton(text, () => { action(); return Task.CompletedTask; }, primary);
    private Button ActionButton(string text, Func<Task> action, bool primary = false)
    {
        var button = new Button { Text = text, FontSize = 14, MinimumHeightRequest = 48, MinimumWidthRequest = 48, Padding = new Thickness(12, 4), CornerRadius = 12, BackgroundColor = primary ? Primary : Tonal, TextColor = primary ? Colors.White : Foreground };
        button.Clicked += async (_, _) =>
        {
            try { await action(); }
            catch (OperationCanceledException) { }
            catch (Exception error) { await DisplayAlertAsync("Could not finish", error.Message, "OK"); }
        };
        return button;
    }
    private void ApplyTheme(IView view)
    {
        BackgroundColor = Surface;
        if (view is Label label) label.TextColor = label == title ? Foreground : Muted;
        if (view is Slider slider) { slider.MinimumTrackColor = Primary; slider.ThumbColor = Primary; slider.MaximumTrackColor = Tonal; }
        if (view is Picker picker) picker.TextColor = Foreground;
        if (view is Button button) { button.BackgroundColor = Tonal; button.TextColor = Foreground; }
        if (view is Layout layout) foreach (var child in layout.Children) ApplyTheme(child);
        if (view is ScrollView scroll && scroll.Content is { } childContent) ApplyTheme(childContent);
        Refresh();
    }
    private void SetMode(bool animation)
    {
        StopPlayback(); session.Cancel(); animate = animation;
        session.EndState = false; session.Tool = DrawingTool.Select;
        updating = true; scrubber.Value = 0; updating = false;
        drawScroll.IsVisible = !animate; animationTools.IsVisible = animate;
        canvas.ShowTrigger = animate; Refresh();
    }
    private void SelectState(bool endState)
    {
        StopPlayback(); session.Cancel(); session.EndState = endState; session.Tool = DrawingTool.Select;
        updating = true; scrubber.Value = endState ? 1 : 0; updating = false; Refresh();
    }
    private void Refresh()
    {
        updating = true;
        Highlight(drawMode, !animate, true); Highlight(animateMode, animate, true);
        Highlight(start, !session.EndState && !canvas.IsPlaying, true); Highlight(end, session.EndState && !canvas.IsPlaying, true);
        Highlight(export, true); Highlight(play, true);
        foreach (var (tool, button) in tools) Highlight(button, session.Tool == tool, true);
        undo.IsEnabled = session.History.CanUndo; redo.IsEnabled = session.History.CanRedo;
        trigger.Text = session.Tool == DrawingTool.Trigger ? "Tap canvas…" : session.Document.Trigger is null ? "Set trigger" : "Edit trigger";
        title.Text = session.Document.Name == "Untitled sketch" ? "Pen Sketch" : session.Document.Name;
        title.LineBreakMode = LineBreakMode.TailTruncation;
        viewport.Text = $"{session.Document.Width} × {session.Document.Height}";
        SemanticProperties.SetDescription(viewport, "Canvas viewport: " + CanvasViewport.Describe(session.Document.Width, session.Document.Height));
        duration.SelectedIndex = Array.IndexOf(new[] { 300, 600, 1000, 1500, 2000, 3000 }, session.Document.DurationMs);
        hint.Text = animate ? session.EndState ? "End state · drag, resize, or fade a selected object." : "Start state · set a trigger, then edit the End state."
            : session.Tool == DrawingTool.Pen ? "Draw freely. Pen pressure controls the stroke width."
            : session.Tool == DrawingTool.Select ? "Drag to move; corner to resize. Two fingers zoom/pan."
            : session.Tool == DrawingTool.Text ? "Tap the canvas to add text by keyboard, pen, or voice."
            : session.Tool == DrawingTool.Eraser ? "Drag over objects or ink strokes to erase them."
            : "Drag on the canvas to draw a shape.";
        RefreshSelection(); canvas.InvalidateSurface(); updating = false;
    }
    private static void Highlight(Button button, bool selected, bool announcesSelection = false)
    {
        button.BackgroundColor = selected ? Primary : Tonal;
        button.TextColor = selected ? Colors.White : Foreground;
        if (announcesSelection)
        {
            SemanticProperties.SetDescription(button, button.Text + (selected ? ", selected" : ", not selected"));
            if (button.Handler?.PlatformView is Android.Views.View native) native.Selected = selected;
        }
    }
    private SketchElement? SelectedElement() => session.Document.At(session.EndState ? 1 : 0).FirstOrDefault(e => e.Id == session.Selected);
    private void RefreshSelection()
    {
        var element = SelectedElement();
        if (!textSizeDragging)
        {
            var wasUpdating = updating; updating = true;
            textSize.SetSelection(element?.Kind == ElementKind.Text && !canvas.IsPlaying
                ? TextLayout.Fit(element.Text, element.Bounds, element.FontSize).FontSize : null);
            updating = wasUpdating;
        }
        opacityRow.IsVisible = animate;
        opacity.IsEnabled = element is not null && !canvas.IsPlaying;
        if (!opacityDragging && element is not null)
        {
            var wasUpdating = updating; updating = true; opacity.Value = element.Opacity; updating = wasUpdating;
        }
        ((Button)selectionTools.Children[4]).IsEnabled = !canvas.IsPlaying;
        ((Button)selectionTools.Children[2]).IsVisible = element?.Kind == ElementKind.Text && !animate;
        ((Button)selectionTools.Children[3]).IsVisible = element is not null && !animate;
        ((Button)selectionTools.Children[4]).IsVisible = element is not null;
    }
    private async void Save()
    {
        if (busy || saveBlocked) return;
        try { await store.SaveAsync(session.History.Current); status.Text = "Saved on this phone"; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { status.Text = "Save failed. Export a copy."; }
    }
    private async Task EnterTextAsync(InkPoint point, SketchElement? element = null)
    {
        var editor = new Editor { Text = element?.Text ?? "", Placeholder = "Type, write with your S Pen, or use the microphone", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 160, MaxLength = 300, TextColor = Foreground, BackgroundColor = Tonal, FontSize = 20 };
        var page = new ContentPage { Title = "Sketch text", BackgroundColor = Surface, SafeAreaEdges = SafeAreaEdges.All };
        var saveText = ActionButton("Use text", async () =>
        {
            if (string.IsNullOrWhiteSpace(editor.Text)) return;
            if (element is null) session.AddText(point, editor.Text);
            else session.Commit(session.Document.SetElement(TextLayout.Resize(element with { Text = editor.Text.Trim() }, element.FontSize, session.Document.Width, session.Document.Height), false));
            await Navigation.PopModalAsync(); Refresh(); Save();
        }, true);
        var mic = ActionButton("Microphone", async () =>
        {
            if (Platform.CurrentActivity is MainActivity activity)
            {
                var result = await activity.DictateAsync();
                if (!string.IsNullOrWhiteSpace(result)) editor.Text = string.IsNullOrWhiteSpace(editor.Text) ? result : editor.Text + " " + result;
            }
        });
        page.Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 16, Children =
        {
            new Label { Text = "Text for your sketch", FontSize = 24, TextColor = Foreground },
            new Label { Text = "Your keyboard can convert S Pen handwriting to text. Freehand writing stays available with the Pen tool.", TextColor = Muted, FontSize = 14 },
            editor, mic, saveText, ActionButton("Cancel", async () => await Navigation.PopModalAsync())
        } } };
        await Navigation.PushModalAsync(page);
        editor.Focus();
    }
    private Task EditSelectedTextAsync() => SelectedElement() is { Kind: ElementKind.Text } element ? EnterTextAsync(default, element) : Task.CompletedTask;
    private async Task ViewportAsync()
    {
        var choice = await DisplayActionSheetAsync("Canvas viewport", "Cancel", null, CanvasViewport.Presets.Select(p => p.Label).ToArray());
        if (CanvasViewport.Presets.FirstOrDefault(p => p.Label == choice) is { } preset) ChangeViewport(preset.Width, preset.Height);
    }
    private void ChangeViewport(int width, int height)
    {
        StopPlayback(); session.Cancel();
        session.Commit(CanvasViewport.Resize(session.Document, width, height));
        canvas.ResetView(); Refresh(); Save();
        hint.Text = "Artwork fitted. Two fingers zoom/pan; Fit shows the whole canvas.";
    }
    private async Task TriggerAsync()
    {
        StopPlayback();
        var kind = await DisplayActionSheetAsync("What starts the animation?", "Cancel", null, "Tap / click", "Text input");
        if (kind is null or "Cancel") return;
        var text = await DisplayPromptAsync(kind == "Text input" ? "Text trigger" : "Tap trigger", kind == "Text input" ? "What text is entered?" : "What is tapped?", initialValue: session.Document.Trigger?.Label ?? "", placeholder: kind == "Text input" ? "Search for coffee" : "Open details", maxLength: 80);
        if (string.IsNullOrWhiteSpace(text)) return;
        session.Commit(session.Document with { Trigger = (session.Document.Trigger ?? new AnimationTrigger()) with { Kind = kind == "Text input" ? TriggerKind.Text : TriggerKind.Tap, Label = text.Trim() } });
        session.Tool = DrawingTool.Trigger; Refresh(); hint.Text = "Tap the canvas to place the trigger marker."; Save();
    }
    private async Task PlayAsync()
    {
        if (playback is not null) { StopPlayback(); Refresh(); return; }
        if (session.Document.Trigger is null || session.Document.EndPoses.Length == 0)
        { await DisplayAlertAsync("Set up the animation", "Set a trigger, choose End, then move, resize, or fade an object. Play will show that transition.", "OK"); return; }
        var currentPlayback = new CancellationTokenSource();
        playback = currentPlayback; var cancellation = currentPlayback.Token;
        canvas.IsPlaying = true; play.Text = "Stop"; Refresh();
        var watch = Stopwatch.StartNew();
        var total = Transition.TriggerHoldMs + session.Document.DurationMs + Transition.EndHoldMs;
        try
        {
            while (watch.ElapsedMilliseconds < total)
            {
                canvas.Progress = Transition.Progress(watch.Elapsed.TotalMilliseconds, session.Document.DurationMs);
                scrubber.Value = canvas.Progress;
                await Task.Delay(16, cancellation);
            }
        }
        finally { if (ReferenceEquals(playback, currentPlayback)) { StopPlayback(); Refresh(); } }
    }
    private void StopPlayback(bool resetProgress = true)
    {
        playback?.Cancel(); playback?.Dispose(); playback = null;
        canvas.IsPlaying = false; play.Text = "Play";
        if (resetProgress)
        {
            var wasUpdating = updating; updating = true; scrubber.Value = session.EndState ? 1 : 0; updating = wasUpdating;
        }
        canvas.InvalidateSurface();
    }
    private async Task ExportAsync()
    {
        StopPlayback();
        var choice = await DisplayActionSheetAsync("Export and send to PC", "Cancel", null, "PNG image", "Animated GIF", "Copy description for LLM");
        if (choice is null or "Cancel") return;
        if (choice == "Copy description for LLM") { await Clipboard.SetTextAsync(SketchExport.Describe(session.Document)); status.Text = "Description copied"; return; }
        if (choice == "Animated GIF" && (session.Document.Trigger is null || session.Document.EndPoses.Length == 0))
        { await DisplayAlertAsync("Animation is not ready", "In Animate, set a trigger and change at least one object in the End state.", "OK"); return; }
        var snapshot = session.Document;
        var progress = session.EndState ? 1f : 0f;
        var isGif = choice == "Animated GIF";
        busy = true; export.IsEnabled = false; status.Text = isGif ? "Rendering GIF…" : "Rendering PNG…";
        try
        {
            var directory = Path.Combine(FileSystem.CacheDirectory, "sharing-root"); Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, $"pen-sketch-{DateTime.Now:yyyyMMdd-HHmmss}.{(isGif ? "gif" : "png")}");
            await Task.Run(() =>
            {
                using var stream = File.Create(file);
                if (isGif) SketchExport.Gif(snapshot, stream);
                else stream.Write(SketchExport.Png(snapshot, progress));
            });
            status.Text = "Ready. Choose Quick Share or another app.";
            await Share.Default.RequestAsync(new ShareFileRequest("Send sketch to PC", new ShareFile(file, isGif ? "image/gif" : "image/png")));
        }
        finally { busy = false; export.IsEnabled = true; Save(); }
    }
    private async Task SketchMenuAsync()
    {
        var choice = await DisplayActionSheetAsync("Sketch", "Cancel", null, "Rename", "New sketch", "Load example", "How to send to PC");
        if (choice == "Rename")
        {
            var name = await DisplayPromptAsync("Sketch name", "Name this UI idea", initialValue: session.Document.Name, maxLength: 50);
            if (!string.IsNullOrWhiteSpace(name)) session.Commit(session.Document with { Name = name.Trim() });
        }
        else if (choice is "New sketch" or "Load example")
        {
            if (session.Document.Elements.Length > 0 && !await DisplayAlertAsync("Replace this sketch?", "Export it first if you need a copy. You can undo this replacement until the app closes.", "Replace", "Cancel")) return;
            busy = false; saveBlocked = false;
            session.Commit(choice == "New sketch" ? new() { Width = session.Document.Width, Height = session.Document.Height } : ExampleSketch.Create());
            session.Selected = null; canvas.ResetView(); SetMode(false);
        }
        else if (choice == "How to send to PC")
            await DisplayAlertAsync("Send a sketch to your PC", "Tap Export, choose PNG or GIF, then choose Quick Share in Android's share sheet. Your PC must be available in your sharing app. You can also send through an installed cloud drive or messaging app. Copy description for LLM adds the exact shape and motion details to your clipboard.", "OK");
        Refresh(); Save();
    }
}
