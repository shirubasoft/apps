using System.Globalization;
using Android.Content;
using Android.OS;
using QuietNight.Audio;
using QuietNight.Core.Detection;
using QuietNight.Masking;
using QuietNight.Training;
using QuietNight.Ui;

namespace QuietNight.Night;

/// <summary>
/// Bedtime. A first visit shows only what the app does and the two steps it needs; after that, the volume, the noises to
/// mask and one button to start. During the night: a dark screen that dims itself further, with the masking sound's
/// bands, what it rose for, and the volume and actions within thumb reach. Afterwards: what the night did.
/// </summary>
public sealed class NightPage : ContentPage
{
    private const string FirstNightAskedKey = "first-night-asked";
    private const float VolumeStep = 0.05f;
    private static readonly string[] Sensitivities = ["Gentle", "Normal", "Sensitive"];
    private static readonly string[] SensitivityNotes =
    [
        "Covers only noises well above the room. For deep sleepers.",
        "Covers noises that clearly stand out from the room.",
        "Covers even faint noises. For light sleepers.",
    ];
    private static readonly TimeSpan AfterNightLasts = TimeSpan.FromHours(12);
    private static readonly TimeSpan SlowStart = TimeSpan.FromSeconds(20);

    private readonly VerticalStackLayout stack;
    private readonly Label headline = Theme.State();
    private readonly Label detail = Theme.Body();
    private readonly Label bigClock = new() { FontFamily = Theme.Display, FontSize = Theme.LargeText ? 56 : 72, TextColor = Theme.Haze, Margin = new Thickness(0, 4, 0, 0) };
    private readonly Label tonightTitle = Theme.Heading("Tonight");
    private readonly Label tonightBoostValue = Theme.Strong(color: Theme.Moon, size: 15);
    private readonly Button stopAtButton = Theme.Secondary("Off");
    private Disclosure? screenAndMorning;
    private readonly Label glanceLine = Theme.Body(size: 15);
    private readonly Button showAll = Theme.Quiet("Show everything");
    private readonly Label problemTitle = Theme.Strong("The night stopped", Theme.Ember);
    private readonly Label problemText = Theme.Body(color: Theme.Moon);
    private readonly Label problemDetails = Theme.Body(size: 15);
    private readonly Disclosure problemMore;
    private readonly Button problemFix = Theme.Secondary("Let it run all night");
    private readonly Border problemCard;
    private readonly Label lastNightTitle = Theme.Heading("Last night");
    private readonly Label lastNightText = Theme.Body(color: Theme.Moon);
    private readonly VerticalStackLayout lastNightList = new() { Spacing = 4 };
    private readonly Disclosure lastNightTimes;
    private readonly Border lastNight;
    private readonly CheckRow microphone = new(Icons.Microphone, "microphone allowed");
    private readonly CheckRow earbuds = new(Icons.Earbuds, "earbuds connected");
    private readonly CheckRow mediaVolume = new(Icons.Speaker, "phone volume up");
    private readonly CheckRow battery = new(Icons.Battery, "can run all night", compact: true);
    private readonly CheckRow notifications = new(Icons.Bell, "notifications on", compact: true);
    private readonly CheckRow charging = new(Icons.Plug, "charging", compact: true);
    private readonly Disclosure recommended;
    private readonly CheckRow allSet = new(Icons.Check, "", mark: false);
    private readonly Border checks;
    private readonly BandSkyline skyline = new();
    private readonly Label skylineNote = Theme.Body("When a noise you chose starts, only the bands it reaches rise, then settle.", size: 15);
    private readonly View bands;
    private readonly View mask;
    private readonly Label maskNote = Theme.Body("Noises left off are ignored.", size: 15);
    private readonly Label volumeValue = Theme.Strong(color: Theme.Moon);
    private readonly Slider volumeSlider;
    private readonly Button preview = Theme.Quiet("Preview sound");
    private readonly Label volumeNote = Theme.Body("Start with a comfortable, quiet level. It rises by itself when a louder noise starts.", size: 15);
    private readonly Label previewNote = Theme.Body("Playing for 20 seconds, with one rise like the one a cat would bring; the bars show it.", size: 15);
    private readonly Label stopsLine = Theme.Body(size: 15);
    private readonly View axis;
    private readonly Label legend;
    private readonly Label stopNote = Theme.Body("Off: it runs until you stop it.", size: 15);
    private readonly Label sessionLine = Theme.Body(size: 15);
    private readonly Button riseButton = Theme.Quiet("Change");
    private readonly Label glanceVolume = new()
    {
        FontFamily = Theme.Semibold, FontSize = 22, TextColor = Theme.Moon, HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center, IsVisible = false,
    };
    private readonly Button lowerRises = Theme.Quiet("Change the loudest rise", Theme.Haze);
    private readonly Button toastDiscard = Theme.Quiet("Discard", Theme.Haze);
    private readonly Button dim = Theme.Secondary("Dim");
    private readonly View volume;
    private readonly Label barVolumeTitle = Theme.Strong("Bedtime volume", Theme.Haze, 14);
    private readonly Label barVolumeValue = Theme.Strong(color: Theme.Moon, size: 15);
    private readonly Slider barVolume;
    private readonly View barVolumeHeader;
    private readonly Grid barVolumeRow;
    private readonly ChoiceRow boost;
    private readonly Label exactRiseValue = Theme.Body(color: Theme.Moon, size: 15);
    private readonly View exactRise;
    private readonly Disclosure fineTune;
    private readonly Border fineTuneCard;
    private readonly Label tonightRises = Theme.Body(color: Theme.Moon);
    private readonly Label tonightDetected = Theme.Body(size: 15);
    private readonly VerticalStackLayout tonightList = new() { Spacing = 4 };
    private readonly Disclosure tonightTimes;
    private readonly Border tonight;
    private readonly Label nextNightNote = Theme.Body("The noises to mask and the sensitivity apply from the next night. The volume and the loudest rise change at once.", size: 15);
    private readonly VerticalStackLayout nextNightBody = new() { Spacing = 16 };
    private readonly Disclosure nextNight;
    private readonly Border nextNightCard;
    private readonly Disclosure privacy;
    private readonly Button start = Theme.Primary("Start the night");
    private readonly Button save = Theme.Secondary("Save what I just heard");
    private readonly Button stop = Theme.Secondary("Stop");
    private readonly Label toastText = Theme.Body(color: Theme.Moon, size: 15);
    private readonly Button toastAction = Theme.Quiet("Undo");
    private readonly Border toast;
    private readonly Sheet sheet = new();
    private readonly Dimmer dimmer = new();
    private readonly IDispatcherTimer timer;
    private NightSettings settings = NightSettings.Load();
    private (bool Running, bool Starting)? mode;
    private bool? setUp;
    private bool earbudsUrgent;
    private bool microphoneUrgent;
    private bool microphoneBlocked;
    private bool notificationsBlocked;
    private PermissionStatus microphoneStatus = PermissionStatus.Unknown;
    private PermissionStatus notificationStatus = PermissionStatus.Unknown;
    private string? dismissedProblem;
    private string? interrupted;
    private string? spoken;
    private int listedMasked = -1;
    private int ticks;
    private bool showing;
    private bool continueStart;
    private bool askingUnsorted;
    private bool watchingResume;
    private int recommendedLeft = -1;
    private MaskerPreview? playing;
    private Action? toastDoes;
    private Action? toastOther;
    private int toastVersion;

    public NightPage()
    {
        Title = "Night";
        BackgroundColor = Theme.Ink;

        var dismiss = Theme.Quiet("Dismiss");
        problemMore = new Disclosure("Details", problemDetails, small: true);
        problemFix.TextColor = Theme.Lamplight;
        problemFix.HorizontalOptions = LayoutOptions.Start;
        problemFix.Clicked += (_, _) => AskForBatteryExemption();
        problemCard = Theme.Card(problemTitle, problemText, problemFix, problemMore, dismiss);
        dismiss.Clicked += (_, _) =>
        {
            dismissedProblem = interrupted ?? NightSession.Status.Problem;
            interrupted = null;
            problemCard.IsVisible = false;
        };
        problemCard.Stroke = Theme.Ember;
        problemCard.StrokeThickness = 1;

        lastNightTimes = new Disclosure("Show times", lastNightList, small: true);
        lastNight = Theme.Card(lastNightTitle, lastNightText, lastNightTimes);

        battery.Acted += AskForBatteryExemption;
        notifications.Acted += async () => await AskForNotificationsAsync();
        recommended = new Disclosure("Recommended", new VerticalStackLayout { Spacing = 12, Children = { battery, notifications, charging } }, small: true);
        mediaVolume.Acted += RaiseMediaVolume;
        checks = Theme.Card(microphone, earbuds, mediaVolume, recommended, allSet, privacy = new Disclosure("Privacy", Theme.Body(
            "Listening stays on this phone. It holds only the last 20 seconds in memory, and keeps a clip only when you save one, on this phone too. Nothing is sent anywhere.",
            size: 15), small: true) { Summary = "Listening stays on this phone." });

        var low = Theme.Body("Low pitch", size: 15);
        var high = Theme.Body("High pitch", size: 15);
        axis = new VerticalStackLayout
        {
            Spacing = 4,
            Children = { new BoxView { HeightRequest = 1, Color = Theme.Outline }, Theme.Row(low, high) },
        };
        SemanticProperties.SetDescription(axis, "Pitch, from low to high");
        legend = Theme.Body("Blue is the bedtime sound; amber is what it adds to cover a noise.", size: 15);
        bands = new VerticalStackLayout { Spacing = 6, Children = { skyline, axis, legend, skylineNote } };

        var noises = new ToggleRow(["Cats", "Motorcycles", "Cars"], [.. NoiseClasses.Targets.Select(n => !settings.Ignored.Contains(n))],
            [Icons.Cat, Icons.Motorcycle, Icons.Car], ("masked", "not masked"));
        noises.Changed += (i, on) => Change(settings with { Ignored = settings.Ignored.With(NoiseClasses.Targets[i], !on) });
        noises.KeptLast += () =>
        {
            maskNote.Text = "Keep at least one. Switch another on first.";
            maskNote.TextColor = Theme.Lamplight;
            SemanticScreenReader.Announce(maskNote.Text);
        };
        mask = new VerticalStackLayout { Spacing = 10, Children = { Theme.Heading("Noises to mask"), noises, maskNote } };

        volumeSlider = Theme.Slider(0, 1, settings.Volume, "Bedtime volume");
        volumeSlider.ValueChanged += (_, e) => Change(settings with { Volume = (float)e.NewValue });
        preview.Clicked += async (_, _) => await TogglePreviewAsync();
        preview.TextColor = Theme.Lamplight;
        Theme.Describe(preview, "Preview the sound for 20 seconds");
        var volumeHeader = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
        };
        volumeHeader.Add(Theme.Heading("Bedtime volume"), 0);
        if (Theme.LargeText)
        {
            // With large text the preview goes under the heading, so neither squeezes the other.
            volumeHeader.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            volumeHeader.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            volumeHeader.RowSpacing = 6;
            volumeHeader.Add(preview, 0, 1);
            preview.HorizontalOptions = LayoutOptions.Start;
        }
        else volumeHeader.Add(preview, 2);
        preview.VerticalOptions = LayoutOptions.Center;
        volumeValue.VerticalOptions = LayoutOptions.Center;
        // Before bed too the volume has steps beside its slider, and its value at the end of the line.
        var volumeLine = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto),
            },
        };
        volumeValue.MinimumWidthRequest = 52;
        volumeLine.Add(Theme.IconButton(Icons.Minus, "Bedtime volume quieter", Theme.Moon, () => Step(-VolumeStep), out _), 0);
        volumeLine.Add(volumeSlider, 1);
        volumeLine.Add(Theme.IconButton(Icons.Plus, "Bedtime volume louder", Theme.Moon, () => Step(VolumeStep), out _), 2);
        volumeLine.Add(volumeValue, 3);
        volumeValue.HorizontalTextAlignment = TextAlignment.End;
        volume = new VerticalStackLayout { Spacing = 6, Children = { volumeHeader, volumeLine, volumeNote, previewNote } };

        // During the night the volume lives in the bar, with steps a sleepy thumb can hit without dragging.
        barVolume = Theme.Slider(0, 1, settings.Volume, "Bedtime volume");
        barVolume.ValueChanged += (_, e) => Change(settings with { Volume = (float)e.NewValue });
        dim.TextColor = Theme.Moonlight;
        dim.Clicked += (_, _) => dimmer.DimNow();
        Theme.Describe(dim, "Dim the screen now");
        dim.MinimumHeightRequest = Theme.Touch;
        dim.Padding = new Thickness(16, 6);
        var barHeader = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        // Dim sits at the far left, away from the volume steps a sleepy thumb aims for.
        barHeader.Add(dim, 0);
        barHeader.Add(barVolumeTitle, 1);
        barHeader.Add(barVolumeValue, 2);
        barVolumeTitle.HorizontalTextAlignment = TextAlignment.End;
        barVolumeTitle.VerticalOptions = barVolumeValue.VerticalOptions = LayoutOptions.Center;
        barVolumeHeader = barHeader;
        barVolumeRow = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto),
            },
        };
        barVolumeRow.Add(Theme.IconButton(Icons.Minus, "Bedtime volume quieter", Theme.Moon, () => Step(-VolumeStep), out _), 0);
        barVolumeRow.Add(barVolume, 1);
        barVolumeRow.Add(Theme.IconButton(Icons.Plus, "Bedtime volume louder", Theme.Moon, () => Step(VolumeStep), out _), 2);
        // At a glance, where the slider is hidden, the volume sits between its steps.
        barVolumeRow.Add(glanceVolume, 1);


        boost = new ChoiceRow(["Small", "Medium", "Large"], RiseIndex(settings.MaxBoostDb), columns: 1, notes:
        [
            $"Up to +{RiseSizes[0]:F0} dB. Gentle; for light sleepers.",
            $"Up to +{RiseSizes[1]:F0} dB.",
            $"Up to +{RiseSizes[2]:F0} dB, the most it allows.",
        ]);
        boost.Changed += i => Change(settings with { MaxBoostDb = RiseSizes[i] });
        // Any amount between the sizes, one decibel at a time, for those who want it exactly.
        exactRise = Theme.Row(exactRiseValue, new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Theme.IconButton(Icons.Minus, "One decibel smaller", Theme.Moon, () => ExactRise(-1), out _),
                Theme.IconButton(Icons.Plus, "One decibel larger", Theme.Moon, () => ExactRise(1), out _),
            },
        });
        var sensitivity = new ChoiceRow(Sensitivities, (int)settings.Sensitivity, columns: 1, notes: SensitivityNotes);
        sensitivity.Changed += i => Change(settings with { Sensitivity = (Sensitivity)i });
        screenAndMorning = new Disclosure("Screen and morning", new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Theme.Strong("Screen during the night"), ScreenChoice(),
                new BoxView { HeightRequest = 8, Color = Colors.Transparent },
                MorningStop(),
            },
        });
        fineTune = new Disclosure("Sound", new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Theme.Strong("Loudest rise allowed"),
                Theme.Body("How far the masking sound may rise to cover a noise. During a night it changes at once.", size: 15),
                boost, exactRise,
                new BoxView { HeightRequest = 8, Color = Colors.Transparent },
                Theme.Strong("Sensitivity"), sensitivity,
                new BoxView { HeightRequest = 8, Color = Colors.Transparent },
            },
        });
        fineTuneCard = Theme.Card(fineTune, screenAndMorning);
        ShowStopAt();

        tonightTimes = new Disclosure("Show times", tonightList, small: true);
        // Tonight's loudest boost can come down at once, for the night a rise has woken someone.
        // Tonight's loudest rise is one quiet line; changing it, rarely needed, opens a sheet.
        riseButton.TextColor = Theme.Lamplight;
        // At a glance, a rise that woke someone can be lowered without finding the session card.
        lowerRises.Clicked += (_, _) => riseButton.SendClicked();
        riseButton.Clicked += async (_, _) =>
        {
            string[] names = ["Small", "Medium", "Large"];
            var exact = Array.IndexOf(RiseSizes, settings.MaxBoostDb);
            var choice = await sheet.ShowAsync(riseButton, "Loudest rise allowed",
                $"Now +{settings.MaxBoostDb:F0} dB. It changes at once and stays for later nights; lower it if a rise woke you. Settings, Sound, holds any amount between.",
                [.. names.Select((n, i) => new SheetOption($"{n} (+{RiseSizes[i]:F0} dB)", null, i == exact ? SheetStyle.Suggested : SheetStyle.Plain, i == exact ? "Current" : null))]);
            if (choice < 0 || choice == exact) return;
            Change(settings with { MaxBoostDb = RiseSizes[choice] });
            SemanticScreenReader.Announce($"Loudest rise allowed: {names[choice]}.");
        };
        var tonightBoost = Theme.Row(tonightBoostValue, riseButton);
        tonight = Theme.Card(tonightTitle, stopsLine, tonightRises, tonightDetected, tonightTimes, sessionLine, tonightBoost);
        nextNight = new Disclosure("Settings", nextNightBody);
        nextNightCard = Theme.Card(nextNight);

        start.AutomationId = "night-toggle";
        start.Clicked += async (_, _) => await PrimaryAsync();
        save.Clicked += async (_, _) => await SaveRecentAsync();
        stop.Clicked += async (_, _) => await StopAsync();
        save.LineBreakMode = stop.LineBreakMode = LineBreakMode.WordWrap;
        // Both night actions stay quiet, so neither outshines the dark; Save is lamp-lit text, Stop an outline.
        save.TextColor = Theme.Lamplight;
        stop.BackgroundColor = Colors.Transparent;
        stop.BorderColor = Theme.Outline;
        stop.BorderWidth = 1.5;
        Theme.Describe(save, "Save what I just heard, the last 20 seconds");
        showAll.Clicked += (_, _) => dimmer.ShowAll();
        toastAction.Clicked += (_, _) =>
        {
            var does = toastDoes;
            HideToast();
            does?.Invoke();
        };
        toastAction.Padding = new Thickness(12, 10);
        toastDiscard.Padding = new Thickness(12, 10);
        toastDiscard.IsVisible = false;
        toastDiscard.Clicked += (_, _) =>
        {
            var does = toastOther;
            HideToast();
            does?.Invoke();
        };
        Grid.SetColumn(toastDiscard, 1);
        Grid.SetColumn(toastAction, 2);
        toastText.VerticalOptions = LayoutOptions.Center;
        toast = new Border
        {
            BackgroundColor = Theme.Slate, StrokeThickness = 0, Padding = new Thickness(18, 4, 8, 4), Margin = new Thickness(20, 0, 20, 8),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, IsVisible = false,
            Content = new Grid
            {
                ColumnSpacing = 4,
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
                Children = { toastText, toastDiscard, toastAction },
            },
        };
        var actions = new Grid { ColumnSpacing = 12, RowSpacing = 10 };
        // Save and Stop share the width; with large text they stack so neither label is cut.
        if (Theme.LargeText)
        {
            actions.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            actions.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            actions.Add(save, 0, 0);
            actions.Add(stop, 0, 1);
            actions.Add(start, 0, 0);
            Grid.SetRowSpan(start, 2);
        }
        else
        {
            actions.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
            actions.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
            actions.Add(save, 0, 0);
            actions.Add(stop, 1, 0);
            actions.Add(start, 0, 0);
            Grid.SetColumnSpan(start, 2);
        }
        start.VerticalOptions = LayoutOptions.Center;
        var bar = new VerticalStackLayout
        {
            Padding = new Thickness(20, 8, 20, 16), Spacing = 6, BackgroundColor = Theme.Ink, Children = { barVolumeHeader, barVolumeRow, actions },
        };

        stack = Theme.Stack(
            new VerticalStackLayout { Spacing = 6, Margin = new Thickness(0, 16, 0, 0), Children = { headline, detail, bigClock, glanceLine, showAll, lowerRises } },
            problemCard, lastNight, checks, volume, mask, bands, fineTuneCard, tonight, nextNightCard);
        // Room at the end, so the last card can scroll fully clear of the fade above the bar.
        stack.Padding = new Thickness(stack.Padding.Left, stack.Padding.Top, stack.Padding.Right, 48);
        // Content fades out under the bar instead of being cut off.
        var fade = new BoxView
        {
            HeightRequest = 28, VerticalOptions = LayoutOptions.End, InputTransparent = true,
            Background = new LinearGradientBrush([new GradientStop(Colors.Transparent, 0), new GradientStop(Theme.Ink, 1)], new Point(0, 0), new Point(0, 1)),
        };
        var page = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };
        page.Add(new ScrollView { Content = stack }, 0, 0);
        page.Add(fade, 0, 0);
        page.Add(toast, 0, 1);
        page.Add(bar, 0, 2);
        page.Add(sheet, 0, 0);
        Grid.SetRowSpan(sheet, 3);
        Content = page;

        dimmer.Changed += () => Render(NightSession.Status);
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += async (_, _) => await TickAsync();
        ShowSettings();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        showing = false;
        settings = NightSettings.Load();
        ShowSettings();
        dimmer.Touch();
        fineTune.Open = false;
        if (Window is { } window && !watchingResume)
        {
            window.Resumed += OnResumed;
            watchingResume = true;
        }
        if (NightSession.RecoverInterrupted() is { } stoppedAt)
            interrupted = $"The night stopped early, around {Clock(stoppedAt)}. Android may have closed the app to save battery; letting it run all night helps.";
        await RefreshPermissionsAsync();
        RefreshChecks();
        mode = null;
        setUp = null;
        Render(NightSession.Status);
        showing = true;
        timer.Start();
    }

    protected override void OnDisappearing()
    {
        timer.Stop();
        skyline.Rest();
        dimmer.Release();
        StopPreview();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() => sheet.Dismiss() || base.OnBackButtonPressed();

    private async Task TickAsync()
    {
        if (showing && !sheet.IsVisible && !askingUnsorted && NightSession.NextUnsorted() is { } clip)
        {
            // Clips saved from the lock screen are named one at a time; one dismissed stays waiting for next time.
            askingUnsorted = true;
            if (await AskAndKeepAsync(clip.Samples, $"You saved 20 seconds from the lock screen at {Clock(clip.Kept)}. What was it?")) clip.Remove();
            askingUnsorted = false;
        }
        var status = NightSession.Status;
        dimmer.Update(status.Running || NightSession.Starting, mayDim: !sheet.IsVisible);
        // Earbuds, battery, charging and permissions change outside the app, as when the user returns from Android's
        // settings; a check every second keeps the list true.
        if (++ticks % 4 == 0)
        {
            // The lock screen's Quieter and Louder change the saved volume; the page follows, so its next step starts there.
            if (NightSettings.Load().Volume is var stored && Math.Abs(stored - settings.Volume) > 0.001)
            {
                settings = settings with { Volume = stored };
                ShowSettings();
            }
            await RefreshPermissionsAsync();
            RefreshChecks();

        }
        if (playing is not null)
        {
            preview.Text = playing.Rising ? "Rising" : $"Stop ({Math.Max(0, playing.Left.TotalSeconds):F0} s)";
            skyline.Show(playing.Rising ? MaskerPreview.RiseShapeDb : [], settings.MaxBoostDb);
        }
        Render(status);
    }

    private async Task RefreshPermissionsAsync()
    {
        microphoneStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        notificationStatus = Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
            ? await Permissions.CheckStatusAsync<Permissions.PostNotifications>()
            : PermissionStatus.Granted;
        if (microphoneStatus == PermissionStatus.Granted) microphoneBlocked = false;
        if (notificationStatus == PermissionStatus.Granted) notificationsBlocked = false;
    }

    /// <summary>How dark the screen keeps during the night; the darkest suits most, and some people need it brighter.</summary>
    private ChoiceRow ScreenChoice()
    {
        var choice = new ChoiceRow(["Darkest", "Low", "Normal"], (int)Dimmer.Screen, columns: 1, notes:
        [
            "Low while you use it, and darker still after half a minute untouched.",
            "Low while the night runs, without dimming further by itself.",
            "Your usual brightness. The Dim button still darkens it when you want.",
        ]);
        choice.Changed += i =>
        {
            Dimmer.Screen = (NightScreen)i;
            ShowStopAt();
        };
        return choice;
    }

    /// <summary>A time in the morning when the night stops by itself, so waking needs no tap; chosen from a dark sheet.</summary>
    private View MorningStop()
    {
        stopAtButton.TextColor = Theme.Lamplight;
        stopAtButton.Clicked += async (_, _) =>
        {
            TimeSpan[] times = [.. Enumerable.Range(0, 8).Select(i => TimeSpan.FromHours(5.5 + i * 0.5))];
            var current = NightSettings.StopAt;
            var choice = await sheet.ShowAsync(stopAtButton, "Stop by itself in the morning?", "The masking sound and the listening stop at the time you choose.",
                [
                    new SheetOption("Off", null, current is null ? SheetStyle.Suggested : SheetStyle.Plain, current is null ? "Current" : null),
                    .. times.Select(t => new SheetOption(Time(t), Icons.Alarm, t == current ? SheetStyle.Suggested : SheetStyle.Plain, t == current ? "Current" : null)),
                ]);
            if (choice < 0) return;
            NightSettings.StopAt = choice == 0 ? null : times[choice - 1];
            // During a night the new time counts for this night too.
            if (NightSession.Status.Running) NightSession.StopsAt = NightSettings.NextStop(DateTime.Now);
            ShowStopAt();
        };
        ShowStopAt();
        var row = Theme.Row(new VerticalStackLayout
        {
            Spacing = 2,
            Children = { Theme.Strong("Stop by itself in the morning"), stopNote },
        }, stopAtButton);
        return row;
    }

    private void ShowStopAt()
    {
        stopAtButton.Text = (NightSettings.StopAt is { } at ? Time(at) : "Off") + " ›";
        stopNote.Text = NightSettings.StopAt is { } chosen
            ? NightSession.Status.Running ? $"Tonight and after, it stops at {Time(chosen)}." : $"It stops by itself at {Time(chosen)}."
            : "Off: it runs until you stop it.";
        Theme.Describe(stopAtButton, NightSettings.StopAt is { } t ? $"Stops by itself at {Time(t)}. Change" : "Stops by itself: off. Change");
        if (screenAndMorning is not null)
            screenAndMorning.Summary = $"{Dimmer.Screen} screen, " + (NightSettings.StopAt is { } s ? $"stops at {Time(s)}" : "runs until stopped");
    }

    private static string Time(TimeSpan at) => DateTime.Today.Add(at).ToString("t", CultureInfo.CurrentCulture);

    private void ExactRise(float by)
    {
        Change(settings with { MaxBoostDb = Math.Clamp(settings.MaxBoostDb + by, 12, 30) });
        SemanticScreenReader.Announce($"Loudest rise allowed: {settings.MaxBoostDb:F0} decibels");
    }

    /// <summary>The loudest rise each size allows, in dB: small, medium and large.</summary>
    private static readonly float[] RiseSizes = [15, 21, 30];

    private static int RiseIndex(float maxBoostDb) => maxBoostDb <= 16 ? 0 : maxBoostDb <= 23 ? 1 : 2;

    private void Step(float by)
    {
        Change(settings with { Volume = Math.Clamp(settings.Volume + by, 0, 1) });
        SemanticScreenReader.Announce($"Bedtime volume {settings.Volume:P0}");
    }

    private void Change(NightSettings next)
    {
        settings = next;
        settings.Save();
        NightSession.Engine?.SetVolume(settings.Volume);
        NightSession.Engine?.SetMaxBoost(settings.MaxBoostDb);
        playing?.SetVolume(settings);
        ShowSettings();
    }

    private void ShowSettings()
    {
        volumeValue.Text = barVolumeValue.Text = $"{settings.Volume:P0}";
        // Both volume sliders show the same setting, whichever one moved.
        foreach (var slider in new[] { volumeSlider, barVolume })
        {
            if (Math.Abs(slider.Value - settings.Volume) > 0.001) slider.Value = settings.Volume;
            SemanticProperties.SetDescription(slider, $"Bedtime volume, {settings.Volume:P0}");
        }
        var size = settings.MaxBoostDb switch
        {
            <= 16 => "small",
            <= 23 => "medium",
            _ => "large",
        };
        var preset = Array.IndexOf(RiseSizes, settings.MaxBoostDb);
        if (boost.Selected != preset) boost.Show(preset);
        exactRiseValue.Text = $"Exactly +{settings.MaxBoostDb:F0} dB";
        SemanticProperties.SetDescription(exactRiseValue, $"Loudest rise allowed, exactly {settings.MaxBoostDb:F0} decibels");
        tonightBoostValue.Text = $"Loudest rise allowed: {size}";
        Theme.Describe(riseButton, $"Change the loudest rise allowed, now {size}");
        fineTune.Summary = $"{Sensitivities[(int)settings.Sensitivity]} sensitivity, {size} rises";
        nextNight.Summary = $"Masking {settings.Masked()}. {fineTune.Summary}.";
        maskNote.Text = "Noises left off are ignored.";
        maskNote.TextColor = Theme.Haze;
    }

    private void Render(NightStatus status)
    {
        var starting = NightSession.Starting && !status.Running;
        var running = status.Running || starting;
        var ready = microphone.Ok && earbuds.Ok;
        if (mode != (running, starting) || (!running && setUp != ready))
        {
            mode = (running, starting);
            setUp = ready;
            SwitchMode(running, starting, ready);
        }
        Announce(status, starting);
        // Dimmed or glanced, the screen keeps only the time, a word that it is listening, the bands and the controls.
        var view = running ? dimmer.View : NightView.Full;
        // With a screen reader on, the content never folds away by itself; only the brightness drops.
        var glance = view != NightView.Full && !Theme.ScreenReader;
        detail.IsVisible = !glance;
        bigClock.IsVisible = glanceLine.IsVisible = glance;
        showAll.IsVisible = lowerRises.IsVisible = view == NightView.Glance;
        headline.TextColor = glance ? Theme.Haze : Theme.Moon;
        headline.Opacity = glance ? 0.75 : 1;
        if (glance)
        {
            bigClock.Text = Clock(DateTimeOffset.Now);
            var listening = status.Paused ? "Paused."
                : status.LoudestBoostDb > 0 && status.Masking.IsTarget() ? "The masking sound rose for it and eases back by itself."
                : $"Listening. {Room(status.RoomDb)}";
            glanceLine.Text = view == NightView.Dimmed ? $"{listening} Touch the screen to wake it." : listening;
        }
        tonight.IsVisible = nextNightCard.IsVisible = running && !glance;
        // At a glance the time and controls lead; the bands shrink.
        if (running) skyline.HeightRequest = glance ? 64 : 112;
        // At a glance the bands speak for themselves; their labels and legend wait for Show everything.
        axis.IsVisible = legend.IsVisible = !glance;

        // In the glance view only the steps change the volume, so a sleepy swipe cannot drag it.
        barVolume.IsVisible = !glance;
        glanceVolume.IsVisible = glance;
        glanceVolume.Text = $"{settings.Volume:P0}";
        AutomationProperties.SetIsInAccessibleTree(glanceVolume, false);
        barVolumeTitle.IsVisible = barVolumeValue.IsVisible = !glance;
        barVolumeHeader.IsVisible = running;
        if (!running)
        {
            RenderIdle(status);
            return;
        }
        // Tonight is drawn against tonight's own loudest boost, whatever is being prepared for the next night.
        skyline.Show(status.BandBoostDb, status.MaxBoostDb);
        var since = status.Since is { } at ? Clock(at) : null;
        if (starting)
            (headline.Text, detail.Text) = ("Starting", NightSession.StartingFor < SlowStart
                ? "Loading the sound model. The masking sound begins in a few seconds; you can lock the phone now."
                : "This is taking longer than usual. If nothing starts soon, tap Stop and start again.");
        else if (status.Paused)
            (headline.Text, detail.Text) = ("Paused", "Another app is playing sound, so the masking sound is silent. It comes back when that stops.");
        else if (status.LoudestBoostDb > 0 && status.Masking.IsTarget())
            (headline.Text, detail.Text) = ($"Masking {NightSettings.Describe(status.Masking)} sound", $"Easing back by itself. Listening since {since}. {Room(status.RoomDb)}");
        else
            (headline.Text, detail.Text) = ("Listening", $"For {NightSession.ListeningFor}, since {since}. {Room(status.RoomDb)}{Ending()}");
        stopsLine.Text = NightSession.StopsAt is { } ends ? $"Stops by itself at {ends.ToString("t", CultureInfo.CurrentCulture)}." : "";
        stopsLine.IsVisible = stopsLine.Text.Length > 0;
        sessionLine.Text = (AudioRoutes.EarbudsName(Platform.AppContext) is { } buds ? $"Playing in {buds}. " : "")
            + (ModelStore.PersonalInfo is not null ? "Using your own detector." : "Using the prebuilt detector.");
        if (status.Masked.Count == listedMasked) return;
        listedMasked = status.Masked.Count;
        tonightRises.Text = NightSummary.Rises(status.Masked) is { } rises ? $"{rises}." : "The masking sound has not needed to rise yet.";
        tonightDetected.Text = NightSummary.Detected(status.Masked) is { } detected
            ? $"Detected {detected}. It can mistake a sound; save one it got wrong to teach it."
            : "";
        tonightDetected.IsVisible = status.Masked.Count > 0;
        tonightTitle.Text = status.Since is { } began && began.ToLocalTime().Hour is >= 6 and < 18 ? "This session" : "Tonight";
        Fill(tonightList, status.Masked, status.MaxBoostDb);
        tonightTimes.IsVisible = status.Masked.Count > 0;
    }

    /// <summary>When the night will end by itself, and that the screen dims, as far as either is so.</summary>
    private static string Ending()
    {
        var stops = NightSession.StopsAt is { } at ? $" It stops by itself at {at.ToString("t", CultureInfo.CurrentCulture)}." : "";
        return stops + (Dimmer.Screen == NightScreen.Darkest ? " The screen dims by itself." : "");
    }

    /// <summary>Tells a screen reader when the night changes state, but not about each sound, which would never stop.</summary>
    private void Announce(NightStatus status, bool starting)
    {
        var state = starting ? "starting"
            : status.Running ? status.Paused ? "paused" : "listening"
            : status.Problem is not null ? $"stopped:{status.Problem}" : "idle";
        if (state == spoken) return;
        var before = spoken;
        spoken = state;
        if (!showing || before is null) return;
        var words = state switch
        {
            "listening" when before == "starting" => "Listening. The masking sound is playing.",
            "listening" => "Listening again.",
            "paused" => "Paused. Another app is playing sound.",
            _ when status.Problem is { } problem && !status.Running => $"The night stopped. {problem}",
            _ => null,
        };
        if (words is not null) SemanticScreenReader.Announce(words);
    }

    /// <summary>What the microphone hears of the room, in words, which also shows that it is listening.</summary>
    private static string Room(float roomDb) => roomDb switch
    {
        float.NaN => "Learning your room.",
        < -60 => "The room is very quiet.",
        < -45 => "The room is quiet.",
        < -35 => "There is some steady background noise.",
        _ => "The room is fairly noisy.",
    };

    private void RenderIdle(NightStatus status)
    {
        var problem = interrupted ?? status.Problem;
        problemTitle.Text = interrupted is not null ? "The night ended early" : "The night stopped";
        problemText.Text = problem ?? "";
        problemDetails.Text = interrupted is null ? NightSession.ProblemDetails ?? "" : "";
        problemMore.IsVisible = problemDetails.Text.Length > 0;
        problemFix.IsVisible = interrupted is not null && !battery.Ok;
        problemCard.IsVisible = problem is not null && problem != dismissedProblem;
        var last = NightSession.LastNight;
        var recent = last is not null && DateTimeOffset.Now - last.Ended < AfterNightLasts;
        var started = last?.Started.ToLocalTime();
        var daytime = started is { Hour: >= 6 and < 18 };
        lastNight.IsVisible = last is not null;
        if (last is not null)
        {
            lastNightTitle.Text = daytime && started!.Value.Date == DateTime.Today ? "Earlier today"
                : recent ? "Last night"
                : $"Last night, {started!.Value.ToString("d MMM", CultureInfo.CurrentCulture)}";
            var span = $"{Clock(last.Started)} to {Clock(last.Ended)}";
            lastNightText.Text = NightSummary.Rises(last.Masked) is { } rises
                ? $"{span}. {rises}. Detected {NightSummary.Detected(last.Masked)}."
                : $"{span}. The masking sound never needed to rise.";
            if (lastNightList.Children.Count != last.Masked.Count + 1) Fill(lastNightList, last.Masked, last.MaxBoostDb);
            lastNightTimes.IsVisible = last.Masked.Count > 0;
        }
        start.Text = !microphone.Ok ? "Allow the microphone" : !earbuds.Ok ? "Connect earbuds" : "Start the night";
        // The preview plays only through earbuds, so without them its button waits.
        Theme.Enable(preview, true, Colors.Transparent, Theme.Lamplight);
        // Until the first night, the page says what the app does before asking for anything.
        var intro = last is null ? "A soft masking sound plays in your earbuds all night and rises only when a noise you chose starts. " : "";
        var missing = new List<string>();
        if (!microphone.Ok) missing.Add("allow the microphone");
        if (!earbuds.Ok) missing.Add("connect your earbuds");
        if (recent && missing.Count == 0)
            (headline.Text, detail.Text) = (DateTime.Now.Hour is >= 4 and < 12 ? "Good morning" : daytime ? "Session ended" : "Night ended",
                "Here is how it went. Start again whenever you are back in bed.");
        else if (missing.Count > 0)
            (headline.Text, detail.Text) = ("Almost ready", $"{intro}{Capitalised(string.Join(" and ", missing))} to start.");
        else
            (headline.Text, detail.Text) = ("Ready for bed", $"{intro}Start when you lie down, then lock the phone; it keeps going.");
    }

    /// <summary>Lays the page out for before, during or while starting a night, and for before or after setup.</summary>
    private void SwitchMode(bool running, bool starting, bool ready)
    {
        listedMasked = -1;
        var firstTime = NightSession.LastNight is null;
        // Until the two required steps are done, the page is only about them; the settings come once they are.
        var settingsShown = !running && ready;
        problemCard.IsVisible = lastNight.IsVisible = checks.IsVisible = !running;
        volume.IsVisible = fineTuneCard.IsVisible = settingsShown;
        volumeNote.IsVisible = settingsShown && firstTime;
        previewNote.IsVisible = playing is not null;
        // Before bed the bands appear only while the preview plays, rising with it.
        bands.IsVisible = running || playing is not null;
        skylineNote.IsVisible = !running;
        skyline.Awake = running;
        skyline.HeightRequest = running ? 112 : 72;
        barVolumeRow.IsVisible = running;
        start.IsVisible = !running;
        save.IsVisible = stop.IsVisible = running;
        Theme.Enable(save, !starting, Theme.Slate, Theme.Lamplight);
        // Stop works while starting too, so a mistaken start can be undone at once.
        stop.IsEnabled = true;
        fineTuneCard.Content = null;
        Detach(mask, bands, fineTune, screenAndMorning!, nextNightNote, checks);
        if (running)
        {
            StopPreview();
            stack.Children.Insert(stack.Children.IndexOf(lastNight) + 1, checks);
            stack.Children.Insert(stack.Children.IndexOf(checks) + 1, bands);
            nextNightBody.Children.Add(nextNightNote);
            nextNightBody.Children.Add(mask);
            nextNightBody.Children.Add(fineTune);
            nextNightBody.Children.Add(screenAndMorning!);
        }
        else
        {
            // Once set up, the volume and noises come first, and the setup card steps below them.
            var at = stack.Children.IndexOf(volume) + 1;
            stack.Children.Insert(at, bands);
            stack.Children.Insert(at + 1, mask);
            if (ready) stack.Children.Insert(at + 2, checks);
            else stack.Children.Insert(stack.Children.IndexOf(lastNight) + 1, checks);
            fineTuneCard.Content = new VerticalStackLayout { Spacing = 12, Children = { fineTune, screenAndMorning! } };
            skyline.Show([], settings.MaxBoostDb);
        }
        mask.IsVisible = running || settingsShown;
        RefreshChecks();
    }

    private static void Detach(params View[] views)
    {
        foreach (var view in views)
            if (view.Parent is Layout parent) parent.Children.Remove(view);
    }

    private void RefreshChecks()
    {
        var context = Platform.AppContext;
        // The two required steps have no buttons of their own: the bar's button takes them in this order.
        var allowed = microphoneStatus == PermissionStatus.Granted;
        if (allowed) microphoneUrgent = false;
        microphone.Show(allowed, allowed ? "Microphone allowed" : "Allow the microphone",
            allowed ? "" : microphoneBlocked
                ? "Android no longer asks, so the button below opens the app's settings. Listening stays on the phone."
                : "It listens for noises. Listening stays on the phone; nothing is kept unless you save it.",
            null, microphoneUrgent);
        var connected = AudioRoutes.HeadphonesConnected(context);
        if (connected) earbudsUrgent = false;
        earbuds.Show(connected, connected ? AudioRoutes.EarbudsName(context) is { } name ? $"Playing in {name}" : "Earbuds connected" : "Connect your earbuds",
            connected ? "" : "Pair Bluetooth earbuds, or plug in wired ones. It never plays from the speaker, where the microphone would hear it.",
            null, earbudsUrgent);
        var exempt = IgnoringBatteryOptimizations();
        battery.Show(exempt, exempt ? "Can run all night" : "Stop Android pausing it overnight",
            exempt ? "" : "Helps keep Android from stopping the night to save battery.", "Allow");
        var notifying = notificationStatus == PermissionStatus.Granted;
        notifications.Show(notifying, notifying ? "Notifications on" : "Notifications",
            notifying ? "" : "The night on your lock screen, with a Stop button.",
            notificationsBlocked ? "Settings" : "Turn on");
        var plugged = Charging();
        charging.Show(plugged, plugged ? "Charging" : "Put it on its charger",
            plugged ? "" : "Listening all night uses about as much battery as music.");
        // A silent media volume would make the whole night silent, so it is flagged like a required step.
        var loud = MediaVolume() >= 0.2f;
        mediaVolume.Show(loud, loud ? "Phone volume up" : "Turn the phone's volume up",
            loud ? "" : "The phone's media volume is very low, so the masking sound would barely play.", "Turn it up");
        microphone.IsVisible = !microphone.Ok;
        earbuds.IsVisible = !earbuds.Ok;
        mediaVolume.IsVisible = !loud && earbuds.Ok;
        var ready = microphone.Ok && earbuds.Ok;
        // Recommended steps come once the required ones are done, folded into one line; a single one shows its button.
        CheckRow[] optional = [battery, notifications, charging];
        foreach (var row in optional)
            row.IsVisible = !row.Ok;
        var left = optional.Where(r => !r.Ok).ToList();
        // Before the first night the sheet at Start offers these steps, so the card does not ask them first.
        var firstNight = NightSession.LastNight is null && !Preferences.Default.Get(FirstNightAskedKey, false);
        recommended.IsVisible = ready && left.Count > 0 && !firstNight;
        recommendedLeft = left.Count;
        recommended.Title = left.Count == 1 ? "1 recommended step" : $"{left.Count} recommended steps";
        recommended.Summary = Capitalised(string.Join(", ", left.Select(r => r.Title.ToLowerInvariant())));
        CheckRow[] rows = [microphone, earbuds, battery, notifications, charging];
        var done = rows.Where(r => r.Ok).Select(r => r.Done).ToList();
        allSet.IsVisible = ready && !firstNight;
        // Privacy sits in the setup card while that has steps to show, and otherwise with the settings, never alone.
        var steps = microphone.IsVisible || earbuds.IsVisible || mediaVolume.IsVisible || recommended.IsVisible || allSet.IsVisible;
        var privacyHome = steps || fineTuneCard.Content is not Microsoft.Maui.Controls.Layout settingsStack
            ? (Microsoft.Maui.Controls.Layout)checks.Content!
            : settingsStack;
        if (privacy.Parent != privacyHome)
        {
            if (privacy.Parent is Microsoft.Maui.Controls.Layout old) old.Children.Remove(privacy);
            privacyHome.Children.Add(privacy);
        }
        // The card shows only while it has something to say.
        checks.IsVisible = mode is not { Running: true } && steps;
        if (!ready) return;
        var all = done.Count == rows.Length;
        // Charging is a state, not a step taken, so it is only mentioned while it is missing.
        done.Remove(charging.Done);
        // A check mark only when nothing is left, so a partial list never reads as a finished step.
        allSet.IconVisible = all;
        var detector = ModelStore.PersonalInfo is { } info
            ? $" Using your own detector, taught {info.Trained.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture)}."
            : "";
        allSet.Show(true, all ? "All set for tonight" : "Already in place", Capitalised(string.Join(", ", done)) + "." + detector);
    }

    private static bool Offered(string step) => Preferences.Default.Get($"offered-{step}", false);

    private static void Offer(string step) => Preferences.Default.Set($"offered-{step}", true);

    /// <summary>Back from Android's battery dialog, setup carries on to the next step or the night, whatever the answer.</summary>
    private async void OnResumed(object? sender, EventArgs e)
    {
        if (!continueStart) return;
        continueStart = false;
        await RefreshPermissionsAsync();
        RefreshChecks();
        await PrimaryAsync();
    }

    /// <summary>The bar's main button: one step of setup per tap, as its label says, then starting the night.</summary>
    private async Task PrimaryAsync()
    {
        if (microphoneStatus != PermissionStatus.Granted)
        {
            await AskForMicrophoneAsync();
            return;
        }
        if (!AudioRoutes.HeadphonesConnected(Platform.AppContext))
        {
            earbudsUrgent = true;
            RefreshChecks();
            var choice = await sheet.ShowAsync(start, "Connect your earbuds",
                "Wired earbuds: plug them in and this page notices by itself. Bluetooth earbuds: pair or connect them in Android's settings.",
                [new SheetOption("Open Bluetooth settings", Icons.Earbuds, SheetStyle.Suggested)], "Done");
            if (choice == 0) OpenBluetoothSettings();
            return;
        }
        // A very low media volume would make the whole night silent, so Start asks first.
        if (MediaVolume() < 0.2f)
        {
            var louder = await sheet.ShowAsync(start, "The phone's volume is very low",
                "The masking sound plays at the bedtime volume times the phone's media volume, so it would barely be heard.",
                [new SheetOption("Turn it up", Icons.Speaker, SheetStyle.Suggested)], "Start anyway");
            if (louder == Sheet.Dismissed) return;
            if (louder == 0)
            {
                RaiseMediaVolume();
                return;
            }
        }
        // Before the first night, the steps that keep it running are offered, once; after each, setup carries on by
        // itself, so the night starts without another tap.
        if (!Preferences.Default.Get(FirstNightAskedKey, false) && ((!battery.Ok && !Offered("battery")) || (!notifications.Ok && !Offered("notifications"))))
        {
            List<(SheetOption Option, string Why, Func<Task> Act)> steps = [];
            if (!battery.Ok && !Offered("battery"))
                steps.Add((new SheetOption("Stop Android pausing it overnight", Icons.Battery, SheetStyle.Suggested),
                    "Android may stop apps overnight to save battery.", () =>
                    {
                        // Android's own dialog takes over; whatever the answer, the page carries on when it is back.
                        Offer("battery");
                        continueStart = true;
                        AskForBatteryExemption();
                        return Task.CompletedTask;
                    }));
            if (!notifications.Ok && !Offered("notifications"))
                steps.Add((new SheetOption("Show it on the lock screen", Icons.Bell, SheetStyle.Suggested),
                    "On the lock screen you can stop the night without unlocking.", async () =>
                    {
                        Offer("notifications");
                        await AskForNotificationsAsync();
                        await PrimaryAsync();
                    }));
            if (steps.Count == 2)
            {
                // Both at once: the lock-screen question first, inside the app, then Android's battery page; the night
                // starts when the user is back.
                var both = steps;
                steps = [(new SheetOption("Set both up", Icons.Check, SheetStyle.Suggested), "", async () =>
                {
                    Offer("notifications");
                    await AskForNotificationsAsync();
                    await both[0].Act();
                })];
                steps[0] = (steps[0].Option, string.Join(" ", both.Select(b => b.Why)), steps[0].Act);
            }
            var choice = await sheet.ShowAsync(start, "Before your first night",
                $"{string.Join(" ", steps.Select(s => s.Why))} Android asks once; the night starts right after.",
                [.. steps.Select(s => s.Option)], "Start without");
            if (choice == Sheet.Dismissed) return;
            if (choice >= 0)
            {
                await steps[choice].Act();
                return;
            }
            Preferences.Default.Set(FirstNightAskedKey, true);
        }
        continueStart = false;
        StopPreview();
        NightSession.Start(Platform.AppContext);
        dimmer.Touch();
        Render(NightSession.Status);
        SemanticScreenReader.Announce("The night is starting.");
    }

    private async Task StopAsync()
    {
        var choice = await sheet.ShowAsync(stop, "Stop the night?", "The masking sound and the listening both stop.",
            [new SheetOption("Stop the night", null, SheetStyle.Suggested)], "Keep listening");
        if (choice != 0) return;
        NightSession.Stop(Platform.AppContext);
        SemanticScreenReader.Announce("The night has stopped.");
    }

    /// <summary>Asks for the microphone if needed; returns whether it is allowed.</summary>
    private async Task<bool> AskForMicrophoneAsync()
    {
        if (microphoneStatus == PermissionStatus.Granted) return true;
        if (microphoneBlocked)
        {
            AppInfo.ShowSettingsUI();
            return false;
        }
        microphoneStatus = await Permissions.RequestAsync<Permissions.Microphone>();
        if (microphoneStatus != PermissionStatus.Granted)
        {
            microphoneUrgent = true;
            // Once Android stops asking, only the app's settings page can allow it.
            microphoneBlocked = !Permissions.ShouldShowRationale<Permissions.Microphone>();
            SemanticScreenReader.Announce("The microphone is needed to hear noises.");
        }
        RefreshChecks();
        return microphoneStatus == PermissionStatus.Granted;
    }

    private async Task AskForNotificationsAsync()
    {
        if (notificationsBlocked)
        {
            AppInfo.ShowSettingsUI();
            return;
        }
        notificationStatus = await Permissions.RequestAsync<Permissions.PostNotifications>();
        notificationsBlocked = notificationStatus != PermissionStatus.Granted && !Permissions.ShouldShowRationale<Permissions.PostNotifications>();
        RefreshChecks();
    }

    private async Task TogglePreviewAsync()
    {
        if (playing is not null)
        {
            StopPreview();
            return;
        }
        playing = await MaskerPreview.StartAsync(settings);
        if (playing is null)
        {
            earbudsUrgent = true;
            RefreshChecks();
            ShowToast("Connect your earbuds to hear it. It never plays from the speaker.");
            return;
        }
        var current = playing;
        bands.IsVisible = true;
        current.Ended += () =>
        {
            if (playing != current) return;
            playing = null;
            preview.Text = "Preview sound";
            Theme.Describe(preview, "Preview the sound for 20 seconds");
            if (mode is { Running: false }) bands.IsVisible = false;
        };
        Theme.Describe(preview, "Stop the preview");
        SemanticScreenReader.Announce("Playing the bedtime sound for 20 seconds, with one rise in the middle.");
    }

    private void StopPreview()
    {
        playing?.Dispose();
        playing = null;
        if (mode is { Running: false }) bands.IsVisible = false;
        preview.Text = "Preview sound";
        Theme.Describe(preview, "Preview the sound for 20 seconds");
    }

    private async Task SaveRecentAsync()
    {
        // The sound is taken at the tap, so answering the question slowly cannot push it out of the 20 seconds.
        var samples = NightSession.CaptureRecent();
        if (samples is null)
        {
            ShowToast("Nothing to save yet; the night has only just started.");
            return;
        }
        var status = NightSession.Status;
        var recent = status.Masked.Count > 0 && DateTimeOffset.Now - status.Masked[^1].At < Night.NightEngine.RecentLength
            ? status.Masked[^1].Noise
            : NoiseClass.Other;
        (NoiseClass Noise, string Text, string Icon)[] kinds =
        [
            (NoiseClass.Cat, "A cat", Icons.Cat), (NoiseClass.Motorcycle, "A motorcycle", Icons.Motorcycle),
            (NoiseClass.Car, "A car", Icons.Car), (NoiseClass.Other, "Not a disturbance", Icons.Harmless),
        ];
        // The sound just covered is the likeliest answer, so it comes first, marked as a suggestion.
        kinds = [.. kinds.OrderByDescending(k => k.Noise == recent && recent != NoiseClass.Other)];
        // Right after a masked sound, one tap keeps it under that name; Change in the message reaches the other names.
        if (recent != NoiseClass.Other)
        {
            await KeepAsync(samples, recent, kinds.First(k => k.Noise == recent).Text, offerChange: true);
            return;
        }
        await AskAndKeepAsync(samples, $"The last 20 seconds are kept on this phone, to teach it on the Teach tab. {Loudest(samples)}");
    }

    /// <summary>Asks what a clip holds and keeps it under that name; returns whether it was named.</summary>
    private async Task<bool> AskAndKeepAsync(float[] samples, string detail)
    {
        (NoiseClass Noise, string Text, string Icon)[] kinds =
        [
            (NoiseClass.Cat, "A cat", Icons.Cat), (NoiseClass.Motorcycle, "A motorcycle", Icons.Motorcycle),
            (NoiseClass.Car, "A car", Icons.Car), (NoiseClass.Other, "Not a disturbance", Icons.Harmless),
        ];
        var choice = await sheet.ShowAsync(save, "What did you just hear?", detail, [.. kinds.Select(k => new SheetOption(k.Text, k.Icon))]);
        if (choice < 0) return false;
        await KeepAsync(samples, kinds[choice].Noise, kinds[choice].Text);
        return true;
    }

    /// <summary>Where the loudest moment of a clip is, so the user can tell it caught the sound.</summary>
    private static string Loudest(float[] samples)
    {
        var at = 0;
        for (var i = 1; i < samples.Length; i++)
            if (MathF.Abs(samples[i]) > MathF.Abs(samples[at])) at = i;
        if (MathF.Abs(samples[at]) < 0.003f) return "It is almost silent, so the sound may have been too quiet to catch.";
        var ago = (samples.Length - at) / (double)Core.Analysis.ListeningFormat.SampleRate;
        return ago < 1.5 ? "Its loudest moment was just now." : $"Its loudest moment was about {ago:F0} seconds ago.";
    }

    /// <summary>Saves a clip from the night; if the phone cannot write it, the clip is held so the user can try again.</summary>
    private async Task KeepAsync(float[] samples, NoiseClass noise, string name, bool offerChange = false)
    {
        try
        {
            var saved = await Task.Run(() => NightSession.Save(samples, noise));
            // Saved in one tap, the message offers a different name too.
            Action? change = offerChange ? async () => await RenameAsync(saved) : null;
            var said = noise == NoiseClass.Other ? "not a disturbance" : $"{name.ToLowerInvariant()} sound";
            ShowToast(offerChange ? $"Saved as {said} (the detector's guess)." : $"Saved as {said}.", "Undo", () =>
            {
                ModelStore.Recordings.Delete(saved);
                SemanticScreenReader.Announce("Removed the saved clip.");
            }, other: change is null ? null : ("Change", change));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast("It could not be saved. The clip is held until you retry or discard it.", "Retry", async () => await KeepAsync(samples, noise, name), held: true,
                other: ("Discard", () => SemanticScreenReader.Announce("Discarded.")));
        }
    }

    /// <summary>Gives a clip saved in one tap another name, for when the detector's guess was wrong.</summary>
    private async Task RenameAsync(Core.Recordings.Recording saved)
    {
        (NoiseClass Noise, string Text, string Icon)[] kinds =
        [
            (NoiseClass.Cat, "A cat", Icons.Cat), (NoiseClass.Motorcycle, "A motorcycle", Icons.Motorcycle),
            (NoiseClass.Car, "A car", Icons.Car), (NoiseClass.Other, "Not a disturbance", Icons.Harmless),
        ];
        var choice = await sheet.ShowAsync(save, "What did you just hear?", "The saved clip takes the name you choose.",
            [.. kinds.Select(k => new SheetOption(k.Text, k.Icon, k.Noise == saved.Label ? SheetStyle.Suggested : SheetStyle.Plain, k.Noise == saved.Label ? "Current" : null))]);
        if (choice < 0 || kinds[choice].Noise == saved.Label) return;
        ModelStore.Recordings.Relabel(saved, kinds[choice].Noise);
        ShowToast($"Saved as {kinds[choice].Text.ToLowerInvariant()}{(kinds[choice].Noise == NoiseClass.Other ? "" : " sound")}.");
    }

    /// <summary>
    /// Shows a short message above the bar, with an optional action such as Undo, for half a minute; with a screen
    /// reader on it stays until the next message, so there is time to reach the action.
    /// </summary>
    /// <param name="held">Whether the message holds something, such as an unsaved clip, so it stays until acted on.</param>
    /// <param name="other">A second action, such as Change or Discard.</param>
    private void ShowToast(string text, string? action = null, Action? does = null, bool held = false, (string Text, Action Does)? other = null)
    {
        toastText.Text = text;
        toastAction.Text = action ?? "";
        toastAction.IsVisible = action is not null;
        toastDiscard.Text = other?.Text ?? "";
        toastDiscard.IsVisible = other is not null;
        toastOther = other?.Does;
        toastDoes = does;
        toast.IsVisible = true;
        SemanticScreenReader.Announce(action is null ? text : $"{text} {action} is available above the Save button.");
        var version = ++toastVersion;
        if (Theme.ScreenReader || held) return;
        // At night a message goes sooner, so it does not keep the screen lit.
        var lasts = Theme.Timeout(TimeSpan.FromSeconds(30));
        Dispatcher.DispatchDelayed(lasts, () =>
        {
            if (version == toastVersion) HideToast();
        });
    }

    private void HideToast()
    {
        toastVersion++;
        toastDoes = null;
        toast.IsVisible = false;
    }

    private static void Fill(VerticalStackLayout list, IReadOnlyList<MaskedNoise> masked, float maxBoostDb)
    {
        list.Children.Clear();
        list.Children.Add(Theme.Body("Bars show how far the masking sound rose for each.", size: 15));
        foreach (var noise in masked.Reverse())
            list.Children.Add(MaskedRow(noise, maxBoostDb));
    }

    /// <summary>One detected noise: when, what it sounded like, and a bar for how far the masking sound rose.</summary>
    private static Grid MaskedRow(MaskedNoise noise, float maxBoostDb)
    {
        var icon = noise.Noise switch
        {
            NoiseClass.Cat => Icons.Cat,
            NoiseClass.Motorcycle => Icons.Motorcycle,
            _ => Icons.Car,
        };
        var time = Clock(noise.At);
        var name = NightSettings.Describe(noise.Noise);
        var share = Math.Clamp(noise.LoudestBoostDb / MathF.Max(maxBoostDb, 1), 0.1f, 1);
        var rise = share switch
        {
            < 0.4f => "a little",
            < 0.75f => "moderately",
            _ => "a lot",
        };
        var row = new Grid
        {
            ColumnSpacing = 12,
            MinimumHeightRequest = 36,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(new GridLength(22)),
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(56)),
            },
        };
        var clock = Theme.Body(time, size: 15);
        // A shared minimum keeps the icons in one column while times differ in width.
        clock.MinimumWidthRequest = 76;
        row.Add(clock, 0);
        row.Add(Theme.Icon(icon, Theme.Moonlight, 20), 1);
        row.Add(Theme.Body(Capitalised(name[(name.IndexOf(' ') + 1)..]), Theme.Moon, 15), 2);
        var track = new Grid
        {
            HeightRequest = 6, VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new BoxView { Color = Theme.Slate, CornerRadius = 3 },
                new BoxView { Color = Theme.Lamplight, CornerRadius = 3, HorizontalOptions = LayoutOptions.Start, WidthRequest = 56 * share },
            },
        };
        row.Add(track, 3);
        SemanticProperties.SetDescription(row, $"{time}, detected {name}; the masking sound rose {rise}");
        return row;
    }

    private static string Clock(DateTimeOffset at) => at.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

    private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static bool IgnoringBatteryOptimizations()
    {
        var context = Platform.AppContext;
        var power = (PowerManager?)context.GetSystemService(Context.PowerService);
        return power?.IsIgnoringBatteryOptimizations(context.PackageName) ?? true;
    }

    /// <summary>The phone's media volume, from 0 to 1, which scales everything the masking sound plays.</summary>
    private static float MediaVolume()
    {
        var audio = (Android.Media.AudioManager?)Platform.AppContext.GetSystemService(Context.AudioService);
        if (audio is null) return 1;
        var max = audio.GetStreamMaxVolume(Android.Media.Stream.Music);
        return max <= 0 ? 1 : audio.GetStreamVolume(Android.Media.Stream.Music) / (float)max;
    }

    /// <summary>Raises the media volume to about half, showing Android's own volume panel so the change is visible.</summary>
    private void RaiseMediaVolume()
    {
        var audio = (Android.Media.AudioManager?)Platform.AppContext.GetSystemService(Context.AudioService);
        if (audio is null) return;
        var max = audio.GetStreamMaxVolume(Android.Media.Stream.Music);
        audio.SetStreamVolume(Android.Media.Stream.Music, Math.Max(audio.GetStreamVolume(Android.Media.Stream.Music), max / 2), Android.Media.VolumeNotificationFlags.ShowUi);
        RefreshChecks();
    }

    private static bool Charging()
    {
        // The battery's last broadcast is sticky, so reading it needs no receiver and no permission.
        var battery = Platform.AppContext.RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
        return battery?.GetIntExtra(BatteryManager.ExtraPlugged, 0) > 0;
    }

    private static void AskForBatteryExemption()
    {
        var context = Platform.AppContext;
        var intent = new Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations,
            Android.Net.Uri.Parse($"package:{context.PackageName}"));
        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }

    private static void OpenBluetoothSettings()
    {
        var intent = new Intent(Android.Provider.Settings.ActionBluetoothSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        Platform.AppContext.StartActivity(intent);
    }
}
