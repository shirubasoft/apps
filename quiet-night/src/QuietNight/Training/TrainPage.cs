using System.Globalization;
using QuietNight.Core.Detection;
using QuietNight.Core.Recordings;
using QuietNight.Core.Training;
using QuietNight.Night;
using QuietNight.Ui;

namespace QuietNight.Training;

/// <summary>Teach the app your own noises: record them, then train the detector on the phone.</summary>
public sealed class TrainPage : ContentPage
{
    private static readonly (string Label, NoiseClass Noise)[] Kinds =
        [("Cat", NoiseClass.Cat), ("Motorcycle", NoiseClass.Motorcycle), ("Car", NoiseClass.Car), ("Not a disturbance", NoiseClass.Other)];

    private readonly ChoiceRow kind = new([.. Kinds.Select(k => k.Label)], 0);
    private readonly Button record = Theme.Primary("Record");
    private readonly Label recordState = Theme.Body("Hold the phone where it lies at night and record the noise itself, up to 30 seconds.");
    private readonly ProgressBar level = new() { ProgressColor = Theme.Accent, BackgroundColor = Theme.Raised, HeightRequest = 6 };
    private readonly Label listTitle = Theme.Heading("Your recordings");
    private readonly VerticalStackLayout list = new() { Spacing = 8 };
    private readonly Button train = Theme.Primary("Train");
    private readonly ProgressBar progress = new() { ProgressColor = Theme.Accent, BackgroundColor = Theme.Raised, HeightRequest = 6, IsVisible = false };
    private readonly Label result = Theme.Body(color: Theme.Text);
    private readonly Label modelInfo = Theme.Body();
    private readonly Button reset = Theme.Secondary("Use the prebuilt model again");
    private readonly ClipPlayer player = new();
    private readonly IDispatcherTimer meter;
    private SoundRecorder? recorder;
    private bool training;

    public TrainPage()
    {
        Title = "Train";
        BackgroundColor = Theme.Background;
        record.AutomationId = "record";
        record.Clicked += async (_, _) => await ToggleRecordingAsync();
        train.Clicked += async (_, _) => await TrainAsync();
        reset.Clicked += async (_, _) => await ResetAsync();
        level.IsVisible = false;
        Content = Theme.Scroll(
            Theme.Title("Teach it your noises"),
            Theme.Body("Recordings of your own cat and your own street make detection more accurate. Recordings of harmless sounds, such as a fan or rain, stop them from raising the masker."),
            Theme.Card(Theme.Heading("What are you recording?"), kind, record, level, recordState),
            Theme.Card(listTitle, list),
            Theme.Card(Theme.Heading("Train the detector"), modelInfo, train, progress, result, reset));
        meter = Dispatcher.CreateTimer();
        meter.Interval = TimeSpan.FromMilliseconds(100);
        meter.Tick += async (_, _) => await TickAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    protected override void OnDisappearing()
    {
        player.Stop();
        // Leaving the tab ends a recording, so the microphone is never held by a page nobody sees.
        if (recorder is not null) _ = FinishRecordingAsync();
        base.OnDisappearing();
    }

    private async Task ToggleRecordingAsync()
    {
        if (recorder is not null)
        {
            await FinishRecordingAsync();
            return;
        }
        if (NightSession.Status.Running)
        {
            await DisplayAlertAsync("The night is running", "Stop the night first, or use \"Save the last 20 seconds\" on the Night tab to keep a noise you just heard.", "OK");
            return;
        }
        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted) return;
        try
        {
            player.Stop();
            recorder = new SoundRecorder();
            recorder.Start();
        }
        catch (InvalidOperationException e)
        {
            recorder?.Dispose();
            recorder = null;
            await DisplayAlertAsync("Cannot record", e.Message, "OK");
            return;
        }
        record.Text = "Stop and save";
        kind.SetEnabled(false);
        level.IsVisible = true;
        meter.Start();
    }

    private async Task TickAsync()
    {
        if (recorder is not { } active) return;
        level.Progress = Math.Clamp((active.LevelDb + 70) / 60, 0, 1);
        recordState.Text = $"Recording {active.Elapsed:m\\:ss} of {SoundRecorder.MaxLength:m\\:ss}";
        if (active.Full) await FinishRecordingAsync();
    }

    private async Task FinishRecordingAsync()
    {
        meter.Stop();
        var active = recorder;
        recorder = null;
        if (active is null) return;
        var samples = active.Stop();
        active.Dispose();
        record.Text = "Record";
        kind.SetEnabled(true);
        level.IsVisible = false;
        if (samples.Length < Core.Analysis.ListeningFormat.SampleRate)
        {
            recordState.Text = "That was shorter than a second, so it was not kept.";
            return;
        }
        var noise = Kinds[kind.Selected].Noise;
        await Task.Run(() => ModelStore.Recordings.Save(samples, noise, DateTimeOffset.Now));
        recordState.Text = $"Saved {samples.Length / (double)Core.Analysis.ListeningFormat.SampleRate:F0} s of {Kinds[kind.Selected].Label.ToLowerInvariant()}.";
        Refresh();
    }

    private async Task TrainAsync()
    {
        var recordings = ModelStore.Recordings.List();
        if (recordings.Count == 0 || training) return;
        training = true;
        train.IsEnabled = false;
        reset.IsEnabled = false;
        progress.IsVisible = true;
        progress.Progress = 0;
        result.Text = "Training on the phone. This takes about a minute.";
        try
        {
            var reporter = new Progress<double>(p => progress.Progress = p);
            var (heads, report) = await Task.Run(async () =>
            {
                var audio = recordings.Select(r => new LabeledAudio(ModelStore.Recordings.Load(r), r.Label)).ToList();
                var current = await ModelStore.HeadsAsync();
                var reference = await ModelStore.ReferenceAsync();
                using var model = new SoundModel(await ModelStore.YamnetAsync(), threads: 2);
                // Training learns every noise, including those the user has chosen not to mask tonight.
                var trainer = new PersonalTrainer(model, NightSettings.Load().Listener with { Ignored = default });
                var outcome = trainer.Train(audio, current, reference, reporter);
                if (outcome.Report.Accepted) ModelStore.SavePersonal(outcome.Heads, recordings.Count);
                return outcome;
            });
            result.Text = Describe(report);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or Microsoft.ML.OnnxRuntime.OnnxRuntimeException)
        {
            result.Text = $"Training failed: {e.Message}";
        }
        finally
        {
            training = false;
            progress.IsVisible = false;
            Refresh();
        }
    }

    private async Task ResetAsync()
    {
        if (!await DisplayAlertAsync("Use the prebuilt model?", "The detector trained on your recordings is removed. Your recordings stay.", "Reset", "Cancel")) return;
        ModelStore.Reset();
        result.Text = "Back to the prebuilt model.";
        Refresh();
    }

    private void Refresh()
    {
        var recordings = ModelStore.Recordings.List();
        listTitle.Text = recordings.Count == 0 ? "Your recordings" : $"Your recordings ({recordings.Count})";
        list.Children.Clear();
        if (recordings.Count == 0)
            list.Children.Add(Theme.Body("No recordings yet."));
        foreach (var recording in recordings)
            list.Children.Add(Row(recording));
        train.Text = recordings.Count == 0 ? "Record something to train" : $"Train on {recordings.Count} recording{(recordings.Count == 1 ? "" : "s")}";
        train.IsEnabled = recordings.Count > 0 && !training;
        reset.IsVisible = ModelStore.IsPersonal;
        reset.IsEnabled = !training;
        modelInfo.Text = ModelStore.PersonalInfo is { } info
            ? $"Using your detector, trained on {info.Recordings} recording{(info.Recordings == 1 ? "" : "s")} on {info.Trained.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}. It starts with the next night."
            : "Using the prebuilt detector: Google's YAMNet sound model, fine-tuned with heads trained on reference cats, motorcycles, cars and everyday sounds from Freesound.";
    }

    private Grid Row(Recording recording)
    {
        var name = Kinds.First(k => k.Noise == recording.Label).Label;
        var text = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                Theme.Body(name, Theme.Text, 15),
                Theme.Body($"{recording.Created.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}  ·  {recording.Duration.TotalSeconds:F0} s", size: 12),
            },
        };
        var play = Theme.Secondary("Play");
        play.Clicked += async (_, _) =>
        {
            if (player.IsPlaying)
            {
                player.Stop();
                return;
            }
            await player.PlayAsync(await Task.Run(() => ModelStore.Recordings.Load(recording)));
        };
        var delete = Theme.Secondary("Delete");
        delete.TextColor = Theme.Danger;
        delete.Clicked += async (_, _) =>
        {
            if (!await DisplayAlertAsync("Delete recording?", $"{name}, {recording.Duration.TotalSeconds:F0} s", "Delete", "Cancel")) return;
            ModelStore.Recordings.Delete(recording);
            Refresh();
        };
        var grid = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
        };
        grid.Add(text, 0);
        grid.Add(play, 1);
        grid.Add(delete, 2);
        return grid;
    }

    private static string Describe(TrainingReport report)
    {
        static string Percent(double value) => double.IsNaN(value) ? "n/a" : value.ToString("P0", CultureInfo.CurrentCulture);
        var before = Mean(report.SoundBefore, report.FrameBefore);
        var after = Mean(report.SoundAfter, report.FrameAfter);
        var summary = $"On held-back parts of your recordings: {Percent(before)} → {Percent(after)} recognised. Reference sounds: {Percent(report.ReplayBefore)} → {Percent(report.ReplayAfter)}.";
        return report.Accepted
            ? $"Saved. {summary} The new detector starts with the next night."
            : $"Not saved, because it did not do better than the current detector. {summary} More or longer recordings usually help.";
    }

    private static double Mean(double a, double b) => double.IsNaN(a) ? b : double.IsNaN(b) ? a : (a + b) / 2;
}
