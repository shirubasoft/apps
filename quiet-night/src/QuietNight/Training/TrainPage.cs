using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using QuietNight.Core.Detection;
using QuietNight.Core.Recordings;
using QuietNight.Core.Training;
using QuietNight.Night;
using QuietNight.Ui;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace QuietNight.Training;

/// <summary>
/// Teach the app your own noises in three steps: choose what to record, record and check it, then teach the detector
/// on the phone. The action for step two always sits in the bar at the bottom, as Start does on the Night tab.
/// Recordings are listed last, where they can be played, relabelled or deleted.
/// </summary>
public sealed class TrainPage : ContentPage
{
    private const int ListedAtFirst = 5;

    /// <summary>Below this peak a take is almost silent, about 50 dB under full scale.</summary>
    private const float SilentPeak = 0.003f;

    private static readonly (string Label, NoiseClass Noise, string Icon, string Note)[] Kinds =
    [
        ("Cat", NoiseClass.Cat, Icons.Cat, "Meows, yowls, hisses and fights."),
        ("Motorcycle", NoiseClass.Motorcycle, Icons.Motorcycle, "Motorcycles passing, revving or pulling away."),
        ("Car", NoiseClass.Car, Icons.Car, "Cars passing or idling outside."),
        ("Not a disturbance", NoiseClass.Other, Icons.Harmless, "Sounds to ignore, such as fans, rain, a heater or traffic hum. They teach it what never to cover."),
    ];

    private readonly VerticalStackLayout stack;
    private readonly ScrollView scroll;
    private readonly ChoiceRow kind = new([.. Kinds.Select(k => k.Label)], 0, [.. Kinds.Select(k => k.Icon)], columns: 2);
    private readonly Label kindNote = Theme.Body(size: 15);
    private readonly VerticalStackLayout kindHome = new() { Spacing = 12 };
    private readonly Border stepOne;
    private readonly Label recordState = Theme.Body(size: 15, color: Theme.Moon);
    private readonly Label clock = new() { FontFamily = Theme.Display, FontSize = 40, TextColor = Theme.Moon };
    private readonly ProgressBar level = new() { ProgressColor = Theme.Moonlight, BackgroundColor = Theme.Slate, HeightRequest = 8 };
    private readonly View listening;
    private readonly Label reviewTitle = Theme.Strong();
    private readonly Label reviewPosition = Theme.Body(size: 15);
    private readonly Label quality = Theme.Body(color: Theme.Ember, size: 15);
    private readonly LoudnessStrip strip = new();
    private readonly Button fromLoudest = Theme.Quiet("Play from its loudest moment");
    private readonly VerticalStackLayout reviewKind = new() { Spacing = 8 };
    private readonly Path reviewPlayIcon = Theme.Icon(Icons.Play, Theme.Moon, 24);
    private readonly Button reviewPlay;
    private readonly View review;
    private readonly Label modelInfo = Theme.Body(color: Theme.Moon);
    private readonly Label advice = Theme.Body(size: 15);
    private readonly Button train = Theme.Primary("Teach it");
    private readonly Border stepThree;
    private readonly ProgressBar progress = new() { ProgressColor = Theme.Lamplight, BackgroundColor = Theme.Slate, HeightRequest = 8, IsVisible = false };
    private readonly Label progressNote = Theme.Body(size: 15);
    private readonly Label resultTitle = Theme.Strong();
    private readonly Label resultText = Theme.Body(color: Theme.Moon, size: 15);
    private readonly Label resultDetails = Theme.Body(size: 15);
    private readonly Disclosure resultMore;
    private readonly Border result;
    private readonly Button reset = Theme.Quiet("Go back to the prebuilt detector");
    private readonly Label listTitle = Theme.Heading("Your recordings");
    private readonly Label counts = Theme.Body(size: 15);
    private readonly VerticalStackLayout list = new() { Spacing = 12 };
    private readonly Button showAll = Theme.Quiet("");
    private readonly Grid stepTwo = Step(2, "Record it");
    private readonly Label blockedNote = Theme.Body("A night is running, so recording waits until it stops.", size: 15);
    private readonly Label barNote = Theme.Body("Recording waits until the night ends.", size: 15);
    private readonly Button toNight = Theme.Quiet("Go to Night");
    private readonly Button toList = Theme.Quiet("Your recordings");
    private readonly Label toastText = Theme.Body(color: Theme.Moon, size: 15);
    private readonly Button toastUndo = Theme.Quiet("Undo");
    private readonly Border toast;
    private Border? listCard;
    private Action? undo;
    private int toastVersion;
    private readonly Button primary = Theme.Primary("Record");
    private readonly Button secondary = Theme.Secondary("Discard");
    private readonly Sheet sheet = new();
    private readonly ClipPlayer player = new();
    private readonly IDispatcherTimer meterTimer;
    private readonly IDispatcherTimer playTimer;
    private SoundRecorder? recorder;
    private float[]? pending;
    private bool training;
    private bool listAll;
    private bool justKept;
    private string? kept;
    private (Path Icon, Button Button, string Name, Label? Position, TimeSpan Length, DateTimeOffset Started, string Resting)? playing;

    public TrainPage()
    {
        Title = "Teach";
        BackgroundColor = Theme.Ink;
        progressNote.IsVisible = false;
        kind.Changed += _ => ShowRecorder();
        kindHome.Children.Add(kind);
        kindHome.Children.Add(kindNote);

        var loudness = Theme.Body("Loudness", size: 15);
        var meter = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            Children = { loudness, level },
        };
        Grid.SetColumn(level, 1);
        level.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetDescription(level, "Loudness the microphone hears");
        listening = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                clock, meter,
                Theme.Body($"The screen stays on while it records, for up to {SoundRecorder.MaxWait.TotalMinutes:F0} minutes. Stay on this tab; leaving it ends the recording.", size: 15),
            },
        };

        reviewPlay = Theme.Describe(new Button { BackgroundColor = Theme.Slate, CornerRadius = 24, WidthRequest = Theme.Touch, HeightRequest = Theme.Touch, Padding = 0 },
            "Play the new recording");
        reviewPlay.Clicked += async (_, _) => await TogglePlayAsync(reviewPlayIcon, reviewPlay, "the new recording", reviewPosition, "Played", () => pending);
        // A tap on the strip plays from there, so a brief sound late in a long take is quick to check.
        // The same seeking as the strip, as a button anyone can reach, screen reader or not.
        fromLoudest.Clicked += async (_, _) =>
        {
            if (pending is null || recorder is not null || NightSession.Status.Running) return;
            StopPlaying();
            var at = (float)(Loudest(pending) / (pending.Length / (double)Core.Analysis.ListeningFormat.SampleRate));
            await TogglePlayAsync(reviewPlayIcon, reviewPlay, "the new recording", reviewPosition, "Played", () => pending, from: Math.Max(0, at - 0.05f));
        };
        strip.Seek += async at =>
        {
            if (pending is null || recorder is not null || NightSession.Status.Running) return;
            StopPlaying();
            await TogglePlayAsync(reviewPlayIcon, reviewPlay, "the new recording", reviewPosition, "Played", () => pending, from: at);
        };
        var reviewHeader = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
        };
        reviewHeader.Add(new Grid { WidthRequest = Theme.Touch, HeightRequest = Theme.Touch, Children = { reviewPlay, reviewPlayIcon } }, 0);
        reviewHeader.Add(new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { reviewTitle, reviewPosition } }, 1);
        review = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                reviewHeader, strip, fromLoudest,
                Theme.Body("Play it, or tap the strip to hear from there. Then choose what it is.", size: 15),
                quality,
                reviewKind,
            },
        };
        train.Clicked += async (_, _) => await TrainAsync();
        reset.Clicked += async (_, _) => await ResetAsync();
        resultMore = new Disclosure("Details", resultDetails, small: true);
        result = Theme.Card(Theme.Slate, resultTitle, resultText, resultMore);
        result.IsVisible = false;
        showAll.Clicked += (_, _) =>
        {
            listAll = !listAll;
            Refresh();
        };
        primary.AutomationId = "record";
        primary.Clicked += async (_, _) => await PrimaryAsync();
        secondary.Clicked += async (_, _) => await SecondaryAsync();
        secondary.BackgroundColor = Colors.Transparent;
        secondary.BorderColor = Theme.Outline;
        secondary.BorderWidth = 1.5;

        var about = new Disclosure("About the detector", Theme.Body(
            "The prebuilt detector is Google's YAMNet sound model, fine-tuned on reference cats, motorcycles, cars and everyday sounds from Freesound. Teaching adjusts its last layers to your recordings, on the phone.",
            size: 15), small: true);
        stepOne = Theme.Card(Step(1, "What will you record?"), kindHome);
        stepThree = Theme.Card(Step(3, "Teach it"), modelInfo, advice, train, progress, progressNote, result, reset, about);
        stack = Theme.Stack(
            Theme.Title("Teach it your noises"),
            Theme.Body("Record your own cat or street to help the detector recognise them. Recordings stay on this phone."),
            toList,
            stepOne,
            Theme.Card(stepTwo, recordState, blockedNote, toNight, listening, review),
            stepThree,
            listCard = Theme.Card(listTitle, counts, list, showAll));
        toList.Clicked += async (_, _) => await scroll!.ScrollToAsync(listCard, ScrollToPosition.Start, !Theme.ReducedMotion);
        scroll = new ScrollView { Content = stack };
        var bar = new Grid { Padding = new Thickness(20, 8, 20, 16), ColumnSpacing = 12, RowSpacing = 10, BackgroundColor = Theme.Ink };
        if (Theme.LargeText)
        {
            bar.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            bar.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            bar.Add(primary, 0, 0);
            bar.Add(secondary, 0, 1);
        }
        else
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
            bar.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
            bar.Add(primary, 0, 0);
            bar.Add(secondary, 1, 0);
        }
        primary.LineBreakMode = secondary.LineBreakMode = LineBreakMode.WordWrap;
        toNight.Clicked += async (_, _) => await Shell.Current.GoToAsync("//night");
        toastUndo.Padding = new Thickness(12, 10);
        toastUndo.Clicked += (_, _) =>
        {
            var undoing = undo;
            HideToast();
            undoing?.Invoke();
        };
        toast = new Border
        {
            BackgroundColor = Theme.Slate, StrokeThickness = 0, Padding = new Thickness(18, 4, 8, 4), Margin = new Thickness(20, 0, 20, 8),
            StrokeShape = new RoundRectangle { CornerRadius = 18 }, IsVisible = false, Content = Theme.Row(toastText, toastUndo),
        };
        barNote.Margin = new Thickness(20, 8, 20, 0);
        barNote.IsVisible = false;
        var page = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
        };
        page.Add(scroll, 0, 0);
        page.Add(toast, 0, 1);
        page.Add(barNote, 0, 2);
        page.Add(bar, 0, 3);
        page.Add(sheet, 0, 0);
        Grid.SetRowSpan(sheet, 4);
        Content = page;
        meterTimer = Dispatcher.CreateTimer();
        meterTimer.Interval = TimeSpan.FromMilliseconds(100);
        meterTimer.Tick += async (_, _) => await TickAsync();
        playTimer = Dispatcher.CreateTimer();
        playTimer.Interval = TimeSpan.FromMilliseconds(200);
        playTimer.Tick += (_, _) => ShowPosition();
        ShowRecorder();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ShowRecorder();
        try
        {
            Refresh();
            // Heads trained on an earlier sound model no longer fit its features; the recordings stay to train again.
            await ModelStore.ForgetIncompatibleAsync();
            Refresh();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowResult(false, "Could not check your detector", "Your recordings are safe. Close the app and open it again.", e.Message);
        }
    }

    protected override void OnDisappearing()
    {
        StopPlaying();
        // Leaving the tab ends a recording, so the microphone is never held by a page nobody sees.
        if (recorder is not null) FinishRecording();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() => sheet.Dismiss() || base.OnBackButtonPressed();

    private static Grid Step(int number, string text)
    {
        var badge = new Border
        {
            BackgroundColor = Theme.Slate, StrokeThickness = 0, StrokeShape = new Ellipse(), WidthRequest = 30, HeightRequest = 30,
            Content = new Label
            {
                Text = number.ToString(CultureInfo.CurrentCulture), FontFamily = Theme.Semibold, FontSize = 15, TextColor = Theme.Lamplight,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            },
        };
        AutomationProperties.SetIsInAccessibleTree(badge, false);
        var heading = Theme.Heading(text);
        SemanticProperties.SetDescription(heading, $"Step {number}: {text}");
        var row = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
        row.Add(badge, 0);
        row.Add(heading, 1);
        heading.VerticalOptions = LayoutOptions.Center;
        return row;
    }

    /// <summary>
    /// Shows step two and the bar as they should be now: ready to listen, listening, checking a new recording, or blocked
    /// by a running night.
    /// </summary>
    private void ShowRecorder()
    {
        var chosen = Kinds[kind.Selected];
        kindNote.Text = chosen.Note;
        var name = Named(chosen.Noise);
        var active = recorder is not null;
        var checking = pending is not null;
        var blocked = !active && !checking && NightSession.Status.Running;
        // While a new recording is checked, its kind is chosen right beside it rather than back in step one.
        var home = checking ? reviewKind : kindHome;
        if (kind.Parent != home)
        {
            if (kind.Parent is Layout old) old.Children.Remove(kind);
            home.Children.Insert(0, kind);
        }
        // Listening and checking are focused stages: what is not about the take in hand folds away until it is done.
        stepOne.IsVisible = !checking && !active;
        // While a take waits, step two is about checking it.
        if (stepTwo.Children.OfType<Label>().FirstOrDefault() is { } heading)
        {
            heading.Text = checking ? "Check it" : "Record it";
            SemanticProperties.SetDescription(heading, $"Step 2: {heading.Text}");
        }
        stepThree.IsVisible = !checking && !active;
        if (listCard is not null) listCard.IsVisible = !checking && !active;
        toList.IsVisible = !checking && !active && ModelStore.Recordings.List().Count > 0;
        kind.SetEnabled(!active);
        listening.IsVisible = active;
        review.IsVisible = checking;
        recordState.IsVisible = !checking;
        if (checking)
        {
            var seconds = Math.Round(pending!.Length / (double)Core.Analysis.ListeningFormat.SampleRate);
            reviewTitle.Text = $"New recording, {seconds:F0} s";
            fromLoudest.Text = $"Play from its loudest moment ({Loudest(pending):F0} s in)";
            fromLoudest.IsVisible = !Silent(pending);
            reviewPosition.Text = "Not played yet";
            strip.Show(pending);
            quality.Text = Quality(pending) ?? "";
            quality.IsVisible = quality.Text.Length > 0;
            var keepAs = chosen.Noise == NoiseClass.Other ? "not a disturbance" : name;
            // An almost silent take leads with recording again; keeping it stays possible.
            // Keeping is always on the left and letting go on the right; a silent take only changes which one leads.
            (primary.Text, secondary.Text) = Silent(pending) ? ("Keep anyway", "Record again") : ($"Keep as {keepAs}", "Discard");
        }
        else if (active)
        {
            recordState.Text = chosen.Noise == NoiseClass.Other
                ? "Recording. Stop when you have enough; the last 30 seconds are kept."
                : $"Recording. Stop once {name} has made its noise; the last 30 seconds are kept.";
            primary.Text = "Stop and check it";
            secondary.Text = "Discard";
        }
        else
        {
            recordState.Text = justKept && kept is not null ? kept
                : blocked
                ? ""
                : chosen.Noise == NoiseClass.Other
                    ? "Leave the phone where it lies at night and record while the sound plays. Stop when you like: the last 30 seconds are kept."
                    : $"Leave the phone where it lies at night and start recording, then wait on this tab. Once {name} has made its noise, stop: the last 30 seconds are kept."
                        + (chosen.Noise == NoiseClass.Cat ? " Cats rarely call on cue: during a night, Save what I just heard on the Night tab catches them too." : "");
            // Blocked, the button itself says why, so the reason is in view however large the text.
            primary.Text = blocked ? "Go to Night" : justKept ? "Record another" : chosen.Noise == NoiseClass.Other ? "Record it" : $"Record {name}";
            // Right after keeping one, the bar offers the two ways on: another recording, or teaching.
            secondary.Text = "Teach it now";
        }
        blockedNote.IsVisible = toNight.IsVisible = blocked;
        recordState.IsVisible = !checking && !blocked;
        // Blocked by a running night, the bar's button takes the user to it, with the reason above.
        Theme.Enable(primary, true, blocked ? Theme.Slate : Theme.Lamplight, blocked ? Theme.Lamplight : Theme.Ink);
        // A suspect take keeps Keep on the left but quiet; Record again, beside the warning, is the one to press.
        var silent = checking && Silent(pending!);
        Lead(primary, !blocked && !silent);
        Lead(secondary, silent);
        barNote.IsVisible = blocked;
        secondary.IsVisible = active || checking || (justKept && !blocked);
        if (Theme.LargeText) Grid.SetColumnSpan(primary, 1);
        else Grid.SetColumnSpan(primary, secondary.IsVisible ? 1 : 2);
    }

    private async Task PrimaryAsync()
    {
        if (recorder is null && pending is null && NightSession.Status.Running)
        {
            await Shell.Current.GoToAsync("//night");
            return;
        }
        justKept = false;
        if (pending is not null) await KeepAsync();
        else if (recorder is not null) FinishRecording();
        else await StartRecordingAsync();
    }

    /// <summary>The bar's second button: discard while listening or checking, keep a silent take anyway, or teach right after keeping.</summary>
    private async Task SecondaryAsync()
    {
        if (pending is not null && Silent(pending))
        {
            Discard(announce: false);
            await StartRecordingAsync();
        }
        else if (recorder is not null || pending is not null) Discard();
        else if (justKept)
        {
            justKept = false;
            ShowRecorder();
            await scroll.ScrollToAsync(stepThree, ScrollToPosition.Start, !Theme.ReducedMotion);
            await TrainAsync();
        }
    }

    /// <summary>Shows a short message above the bar with Undo, for half a minute, or until the next one with a screen reader on.</summary>
    private void ShowToast(string text, Action undoing)
    {
        toastText.Text = text;
        undo = undoing;
        toast.IsVisible = true;
        SemanticScreenReader.Announce($"{text} Undo is available above the buttons at the bottom.");
        var version = ++toastVersion;
        if (Theme.ScreenReader) return;
        Dispatcher.DispatchDelayed(Theme.Timeout(TimeSpan.FromSeconds(30)), () =>
        {
            if (version == toastVersion) HideToast();
        });
    }

    private void HideToast()
    {
        toastVersion++;
        undo = null;
        toast.IsVisible = false;
    }

    private static bool Silent(float[] samples) => samples.Max(MathF.Abs) < SilentPeak;

    /// <summary>How many seconds into a recording its loudest moment is.</summary>
    private static double Loudest(float[] samples)
    {
        var at = 0;
        for (var i = 1; i < samples.Length; i++)
            if (MathF.Abs(samples[i]) > MathF.Abs(samples[at])) at = i;
        return at / (double)Core.Analysis.ListeningFormat.SampleRate;
    }

    /// <summary>Draws a bar button as the one to press, in lamplight, or as a quiet outline.</summary>
    private static void Lead(Button button, bool lead)
    {
        if (!button.IsEnabled) return;
        button.BackgroundColor = lead ? Theme.Lamplight : Colors.Transparent;
        button.TextColor = lead ? Theme.Ink : Theme.Moon;
        button.BorderColor = lead ? Colors.Transparent : Theme.Outline;
        button.BorderWidth = lead ? 0 : 1.5;
    }

    private void Discard(bool announce = true)
    {
        if (recorder is not null)
        {
            var active = recorder;
            recorder = null;
            meterTimer.Stop();
            Platform.CurrentActivity?.Window?.ClearFlags(Android.Views.WindowManagerFlags.KeepScreenOn);
            active.Stop();
            active.Dispose();
        }
        StopPlaying();
        var dropped = pending;
        pending = null;
        ShowRecorder();
        Refresh();
        recordState.Text = "Discarded. Nothing was kept.";
        // A rare take is worth a second chance: Undo brings it back to the review.
        if (dropped is not null)
            ShowToast("Discarded the take.", () =>
            {
                if (recorder is not null) return;
                pending = dropped;
                ShowRecorder();
                Refresh();
                SemanticScreenReader.Announce("The take is back.");
            });
        else if (announce) SemanticScreenReader.Announce(recordState.Text);
    }

    private async Task StartRecordingAsync()
    {
        if (NightSession.Status.Running)
        {
            ShowRecorder();
            return;
        }
        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            var blocked = !Permissions.ShouldShowRationale<Permissions.Microphone>();
            var choice = await sheet.ShowAsync(primary, "Recording needs the microphone",
                blocked ? "Android no longer asks, so allow it in the app's settings." : "Allow it when Android asks.",
                [new SheetOption(blocked ? "Open app settings" : "Try again", Icons.Microphone, SheetStyle.Primary)]);
            if (choice == 0)
            {
                if (blocked) AppInfo.ShowSettingsUI();
                else await StartRecordingAsync();
            }
            return;
        }
        try
        {
            StopPlaying();
            recorder = new SoundRecorder();
            recorder.Start();
        }
        catch (InvalidOperationException e)
        {
            recorder?.Dispose();
            recorder = null;
            recordState.Text = $"The microphone could not start. Another app may be using it. ({e.Message})";
            return;
        }
        justKept = false;
        clock.Text = "0:00";
        level.Progress = 0;
        ShowRecorder();
        Refresh();
        SemanticScreenReader.Announce("Recording. Stop once the noise has happened.");
        // The wait can be minutes, and a screen that times out would end it, so the screen stays on while it listens.
        Platform.CurrentActivity?.Window?.AddFlags(Android.Views.WindowManagerFlags.KeepScreenOn);
        meterTimer.Start();
    }

    private async Task TickAsync()
    {
        if (recorder is not { } active) return;
        level.Progress = Math.Clamp((active.LevelDb + 70) / 60, 0, 1);
        SemanticProperties.SetDescription(level, $"Loudness the microphone hears: {level.Progress:P0}");
        clock.Text = $"{active.Elapsed:m\\:ss}";
        SemanticProperties.SetDescription(clock, $"Listening for {active.Elapsed.TotalSeconds:F0} seconds");
        if (active.Done) FinishRecording();
        await Task.CompletedTask;
    }

    private void FinishRecording()
    {
        meterTimer.Stop();
        Platform.CurrentActivity?.Window?.ClearFlags(Android.Views.WindowManagerFlags.KeepScreenOn);
        var active = recorder;
        recorder = null;
        if (active is null) return;
        var samples = active.Stop();
        active.Dispose();
        if (samples.Length < Core.Analysis.ListeningFormat.SampleRate)
        {
            ShowRecorder();
            Refresh();
            recordState.Text = "That was shorter than a second, so it was not kept.";
            SemanticScreenReader.Announce(recordState.Text);
            return;
        }
        // A new recording is checked before it is kept, so a wrong label or an empty take never reaches training.
        pending = samples;
        ShowRecorder();
        Refresh();
        // Step one folds away for the review, so the scroll waits for that layout before bringing the review into view.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () => _ = scroll.ScrollToAsync(review, ScrollToPosition.Start, !Theme.ReducedMotion));
        SemanticScreenReader.Announce($"Recorded. {Quality(samples) ?? ""} Play it back, then keep or discard it.");
    }

    private async Task KeepAsync()
    {
        if (pending is not { } samples) return;
        StopPlaying();
        var chosen = Kinds[kind.Selected];
        try
        {
            await Task.Run(() => ModelStore.Recordings.Save(samples, chosen.Noise, DateTimeOffset.Now));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The take stays in review, so the user can try again or discard it.
            quality.Text = $"It could not be saved, so it is still here. Try Keep again, or discard it. ({e.Message})";
            quality.IsVisible = true;
            SemanticScreenReader.Announce("The recording could not be saved. It is still here; try Keep again.");
            return;
        }
        pending = null;
        ShowRecorder();
        var seconds = samples.Length / (double)Core.Analysis.ListeningFormat.SampleRate;
        kept = chosen.Noise == NoiseClass.Other
            ? $"Kept {seconds:F0} s as not a disturbance. Record another, or teach it now."
            : $"Kept {seconds:F0} s of {Named(chosen.Noise)}. Record another, or teach it now.";
        justKept = true;
        ShowRecorder();
        SemanticScreenReader.Announce($"{recordState.Text} Record another or teach it now, at the bottom.");
        Refresh();
    }

    private async Task TrainAsync()
    {
        var recordings = ModelStore.Recordings.List();
        if (recordings.Count == 0 || training || recorder is not null || pending is not null || NightSession.Status.Running) return;
        training = true;
        justKept = false;
        ShowRecorder();
        Theme.Enable(train, false, Theme.Lamplight, Theme.Ink);
        reset.IsEnabled = false;
        progress.IsVisible = progressNote.IsVisible = true;
        progress.Progress = 0;
        progressNote.Text = "Teaching on the phone. This takes about a minute; you can leave this tab.";
        result.IsVisible = false;
        try
        {
            var reporter = new Progress<double>(p =>
            {
                progress.Progress = p;
                progressNote.Text = $"Teaching on the phone: {p:P0}. This takes about a minute; you can leave this tab.";
            });
            var (_, report) = await Task.Run(async () =>
            {
                var audio = recordings.Select(r => new LabeledAudio(ModelStore.Recordings.Load(r), r.Label)).ToList();
                var current = await ModelStore.HeadsAsync();
                var reference = await ModelStore.ReferenceAsync();
                using var model = new SoundModel(await ModelStore.YamnetAsync(), threads: 2);
                // Training learns every noise, including those the user has chosen not to mask tonight.
                var trainer = new PersonalTrainer(model, NightSettings.Load().Listener with { Ignored = default });
                var outcome = trainer.Train(audio, current, reference, reporter);
                if (outcome.Report.Accepted) await ModelStore.SavePersonalAsync(outcome.Heads, recordings.Count);
                return outcome;
            });
            Describe(report);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException)
        {
            ShowResult(false, "Teaching could not finish",
                "Your recordings and the current detector are unchanged. Try again; if it keeps failing, delete the newest recording.", e.Message);
        }
        finally
        {
            training = false;
            progress.IsVisible = progressNote.IsVisible = false;
            Refresh();
        }
    }

    private async Task ResetAsync()
    {
        var choice = await sheet.ShowAsync(reset, "Go back to the prebuilt detector?",
            "The detector taught on your recordings is removed. Your recordings stay, so you can teach it again.",
            [new SheetOption("Go back to the prebuilt detector", null, SheetStyle.Primary)]);
        if (choice != 0) return;
        await ModelStore.ResetAsync();
        ShowResult(true, "Back to the prebuilt detector", "It is used from the next night.", null);
        Refresh();
    }

    private void Refresh()
    {
        var recordings = ModelStore.Recordings.List();
        listTitle.Text = recordings.Count == 0 ? "Your recordings" : $"Your recordings ({recordings.Count})";
        toList.Text = $"Jump to your recordings ({recordings.Count})";
        toList.IsVisible = recordings.Count > 0 && recorder is null && pending is null;
        var byKind = Kinds.Select(k => (k.Label, Count: recordings.Count(r => r.Label == k.Noise))).Where(k => k.Count > 0).ToList();
        counts.Text = recordings.Count == 0
            ? "Nothing recorded yet. What you keep shows up here."
            : string.Join(", ", byKind.Select(k => $"{k.Label} {k.Count}"));
        StopPlaying();
        list.Children.Clear();
        foreach (var recording in listAll ? recordings : recordings.Take(ListedAtFirst))
            list.Children.Add(Row(recording));
        showAll.IsVisible = recordings.Count > ListedAtFirst;
        showAll.Text = listAll ? "Show fewer" : $"Show all {recordings.Count}";
        var taught = ModelStore.PersonalInfo?.Trained;
        var fresh = taught is { } at ? recordings.Count(r => r.Created > at) : recordings.Count;
        var plural = recordings.Count == 1 ? "" : "s";
        train.Text = recordings.Count == 0 ? "Record something first"
            : taught is null ? $"Teach it with {recordings.Count} recording{plural}"
            : fresh > 0 ? $"Teach it with {recordings.Count} recording{plural} ({fresh} new)"
            : "Teach it again (nothing new)";
        var busy = recorder is not null || pending is not null;
        // With nothing new since the last teaching, the button steps back, since teaching again rarely changes much.
        var (face, ink) = taught is not null && fresh == 0 ? (Theme.Slate, Theme.Lamplight) : (Theme.Lamplight, Theme.Ink);
        var night = NightSession.Status.Running;
        Theme.Enable(train, recordings.Count > 0 && !training && !busy && !night, face, ink);
        // Right after keeping a recording the bar offers teaching, so step three's own button steps aside.
        train.IsVisible = !justKept;
        reset.IsVisible = ModelStore.IsPersonal;
        reset.IsEnabled = !training;
        var thin = byKind.Where(k => k.Count == 1).Select(k => k.Label.ToLowerInvariant()).ToList();
        advice.Text = night
            ? "Teaching waits until the night ends, so it never competes with the listening."
            : busy
            ? "Keep or discard the new recording first; teaching uses the recordings listed below."
            : recordings.Count == 0
                ? "Teaching takes about a minute on the phone. It keeps the new detector only if it recognises your recordings better."
                : thin.Count > 0
                    ? $"One more {string.Join(" and ", thin)} recording would help; 3 to 5 of each kind work best. It keeps the new detector only if it does better."
                    : "Takes about a minute on the phone. It keeps the new detector only if it recognises your recordings better.";
        modelInfo.Text = ModelStore.PersonalInfo is { } info
            ? $"Using your detector, taught with {info.Recordings} recording{(info.Recordings == 1 ? "" : "s")} {When(info.Trained)}."
            : "Using the prebuilt detector.";
        RefreshPlayback();
    }

    private Grid Row(Recording recording)
    {
        var name = KindName(recording.Label);
        var when = When(recording.Created);
        var length = TimeSpan.FromSeconds(Math.Round(recording.Duration.TotalSeconds));
        var resting = $"{recording.Duration.TotalSeconds:F0} s, {when}";
        var position = Theme.Body(Capitalised(resting), Theme.Moon, 15);
        var text = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.Start,
            Children = { Pill(name, Kinds.First(k => k.Noise == recording.Label).Icon), position },
        };
        var spoken = $"{name} recording, {recording.Duration.TotalSeconds:F0} seconds, {when}";
        var relabel = Theme.Describe(new Button { BackgroundColor = Colors.Transparent, CornerRadius = 12, Padding = 0 }, spoken, "Double-tap to change what it is, or delete it");
        relabel.Clicked += async (_, _) => await RelabelAsync(recording, relabel);
        var playIcon = Theme.Icon(Icons.Play, Theme.Moon, 24);
        var play = Theme.Describe(new Button { BackgroundColor = Theme.Slate, CornerRadius = 24, WidthRequest = Theme.Touch, HeightRequest = Theme.Touch, Padding = 0 },
            $"Play {spoken}");
        play.Clicked += async (_, _) => await TogglePlayAsync(playIcon, play, spoken, position, Capitalised(resting), () => ModelStore.Recordings.Load(recording), length);
        // The relabel button speaks for the row's words, so a screen reader meets each recording once.
        AutomationProperties.SetIsInAccessibleTree(text, false);
        var actions = new Grid { WidthRequest = Theme.Touch, HeightRequest = Theme.Touch, Children = { play, playIcon } };
        Grid row;
        row = new Grid { ColumnSpacing = 12, RowSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.Add(text, 0, 0);
        row.Add(relabel, 0, 0);
        // With large text the buttons move under the words, so the label, length and date keep the full width.
        if (Theme.LargeText)
        {
            row.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            row.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            row.Add(actions, 0, 1);
        }
        else
        {
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            row.Add(actions, 1, 0);
        }
        return row;
    }

    /// <summary>A recording's label drawn as a pill with a chevron, showing that tapping it changes what it is.</summary>
    private static Border Pill(string name, string iconData) => new()
    {
        BackgroundColor = Theme.Slate,
        Stroke = Theme.Outline,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 18 },
        Padding = new Thickness(14, 6, 8, 6),
        MinimumHeightRequest = 40,
        HorizontalOptions = LayoutOptions.Start,
        Content = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            Children = { Theme.Icon(iconData, Theme.Moonlight, 20), Theme.Strong(name, size: 15), Theme.Icon(Icons.Expand, Theme.Lamplight, 20) },
        },
    };

    /// <summary>After the list is rebuilt, puts screen-reader focus back on the row that was acted on, or the nearest one.</summary>
    private void FocusRow(int index)
    {
        if (list.Children.Count == 0) return;
        var row = list.Children[Math.Clamp(index, 0, list.Children.Count - 1)] as Grid;
        row?.Children.OfType<Button>().FirstOrDefault()?.SetSemanticFocus();
    }

    /// <summary>A warning when a recording is almost silent or so loud it clipped, or null when it sounds usable.</summary>
    private static string? Quality(float[] samples)
    {
        var peak = 0f;
        var clipped = 0;
        foreach (var sample in samples)
        {
            var size = MathF.Abs(sample);
            peak = MathF.Max(peak, size);
            if (size >= 0.99f) clipped++;
        }
        if (peak < SilentPeak) return "This is almost silent, so the microphone may have missed the noise. You can still keep it.";
        if (clipped > samples.Length / 1000) return "This is so loud that it clipped. If you can, record again with the phone a little further away.";
        return null;
    }

    /// <summary>A recording's own sheet: what it is, which can be changed, and deleting it.</summary>
    private async Task RelabelAsync(Recording recording, Button from)
    {
        var index = list.Children.IndexOf((View)from.Parent);
        var choice = await sheet.ShowAsync(from, "What is in this recording?",
            $"{KindName(recording.Label)}, {recording.Duration.TotalSeconds:F0} s, {When(recording.Created)}.",
            [
                .. Kinds.Select(k => new SheetOption(k.Label, k.Icon,
                    k.Noise == recording.Label ? SheetStyle.Suggested : SheetStyle.Plain, k.Noise == recording.Label ? "Current" : null)),
                new SheetOption("Delete this recording", Icons.Delete, SheetStyle.Danger),
            ]);
        if (choice == Kinds.Length)
        {
            // The sound is kept in memory until the Undo goes, so a slip of the thumb costs nothing.
            var samples = await Task.Run(() => ModelStore.Recordings.Load(recording));
            ModelStore.Recordings.Delete(recording);
            Refresh();
            FocusRow(index);
            ShowToast("Deleted the recording.", () =>
            {
                ModelStore.Recordings.Save(samples, recording.Label, recording.Created);
                Refresh();
                SemanticScreenReader.Announce("Restored.");
            });
            return;
        }
        if (choice < 0 || Kinds[choice].Noise == recording.Label) return;
        StopPlaying();
        ModelStore.Recordings.Relabel(recording, Kinds[choice].Noise);
        Refresh();
        FocusRow(index);
        SemanticScreenReader.Announce($"Now labelled {KindName(Kinds[choice].Noise)}.");
    }

    /// <param name="from">Where to start, from 0 to 1 of the way through.</param>
    private async Task TogglePlayAsync(Path icon, Button button, string name, Label position, string resting, Func<float[]?> load, TimeSpan? length = null, float from = 0)
    {
        var wasThis = playing is { } current && current.Button == button;
        StopPlaying();
        if (wasThis || recorder is not null) return;
        var samples = await Task.Run(load);
        if (samples is null) return;
        var total = length ?? TimeSpan.FromSeconds(Math.Round(samples.Length / (double)Core.Analysis.ListeningFormat.SampleRate));
        var skip = (int)(Math.Clamp(from, 0, 1) * samples.Length);
        // Starting part-way, the clock is set back by the part skipped, so the position and playhead stay true.
        playing = (icon, button, name, position, total, DateTimeOffset.Now - TimeSpan.FromSeconds(skip / (double)Core.Analysis.ListeningFormat.SampleRate), resting);
        Swap(icon, Icons.Stop);
        SemanticProperties.SetDescription(button, $"Stop playing {name}");
        playTimer.Start();
        await player.PlayAsync(skip > 0 ? samples[skip..] : samples);
        if (playing is { } still && still.Button == button) StopPlaying();
    }

    /// <summary>Shows how far playback has got, as "0:04 of 0:12", and moves the review strip's playhead.</summary>
    private void ShowPosition()
    {
        if (playing is not { Position: { } label } now) return;
        var at = DateTimeOffset.Now - now.Started;
        if (at > now.Length) at = now.Length;
        label.Text = $"Playing {at:m\\:ss} of {now.Length:m\\:ss}";
        if (label == reviewPosition) strip.Playhead = (float)(at / now.Length);
    }

    private void StopPlaying()
    {
        player.Stop();
        playTimer.Stop();
        strip.Playhead = null;
        if (playing is not { } current) return;
        playing = null;
        Swap(current.Icon, Icons.Play);
        SemanticProperties.SetDescription(current.Button, $"Play {current.Name}");
        if (current.Position is { } label)
            label.Text = current.Resting;
    }

    /// <summary>Play buttons are off while recording, so a clip never plays into the microphone.</summary>
    private void RefreshPlayback()
    {
        var free = recorder is null && !NightSession.Status.Running;
        reviewPlay.IsEnabled = free;
        foreach (var row in list.Children.OfType<Grid>())
            foreach (var button in row.Children.OfType<Layout>().SelectMany(Buttons))
            {
                button.IsEnabled = free;
                button.Opacity = free ? 1 : 0.4;
            }
    }

    private static IEnumerable<Button> Buttons(Layout layout) =>
        layout.Children.SelectMany(child => child switch
        {
            Button button => [button],
            Layout inner => Buttons(inner),
            _ => [],
        });

    private static void Swap(Path icon, string data) =>
        icon.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!;

    private static string KindName(NoiseClass noise) => Kinds.First(k => k.Noise == noise).Label;

    /// <summary>A kind in a sentence: "a cat", or "not a disturbance".</summary>
    private static string Named(NoiseClass noise) => noise switch
    {
        NoiseClass.Cat => "a cat",
        NoiseClass.Motorcycle => "a motorcycle",
        NoiseClass.Car => "a car",
        _ => "the sound",
    };

    /// <summary>When something happened, in words: "today at 2:13 PM", "yesterday at 11:40 PM" or a date.</summary>
    private static string When(DateTimeOffset at)
    {
        var local = at.ToLocalTime();
        var time = local.ToString("t", CultureInfo.CurrentCulture);
        var days = (DateTime.Today - local.Date).Days;
        return days switch
        {
            0 => $"today at {time}",
            1 => $"yesterday at {time}",
            _ => $"{local.ToString("d MMM", CultureInfo.CurrentCulture)}, {time}",
        };
    }

    private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private void Describe(TrainingReport report)
    {
        static string Percent(double value) => double.IsNaN(value) ? "not measured" : value.ToString("P0", CultureInfo.CurrentCulture);
        var before = Mean(report.SoundBefore, report.FrameBefore);
        var after = Mean(report.SoundAfter, report.FrameAfter);
        var details = $"Parts of your recordings held back from teaching: {Percent(before)} recognised before, {Percent(after)} after. " +
            $"Reference sounds it never trained on: {Percent(report.ReplayBefore)} before, {Percent(report.ReplayAfter)} after.";
        if (report.Accepted)
            ShowResult(true, "Your detector improved", $"It now recognises {Percent(after)} of your held-back recordings, up from {Percent(before)}. It is used from the next night.", details);
        else
            ShowResult(false, "Current detector kept", "The new one did not recognise your recordings better, so nothing changed. More or longer recordings usually help.", details);
    }

    private void ShowResult(bool good, string title, string text, string? details)
    {
        resultTitle.Text = title;
        resultTitle.TextColor = good ? Theme.Moonlight : Theme.Lamplight;
        resultText.Text = text;
        resultDetails.Text = details ?? "";
        resultMore.IsVisible = details is not null;
        resultMore.Open = false;
        result.IsVisible = true;
        SemanticScreenReader.Announce($"{title}. {text}");
    }

    private static double Mean(double a, double b) => double.IsNaN(a) ? b : double.IsNaN(b) ? a : (a + b) / 2;
}
