using System.Globalization;
using Android.Content;
using Android.OS;
using QuietNight.Audio;
using QuietNight.Core.Detection;
using QuietNight.Ui;

namespace QuietNight.Night;

/// <summary>Bedtime: start or stop the night, set the masker, and see what was masked.</summary>
public sealed class NightPage : ContentPage
{
    private static readonly string[] Sensitivities = ["Gentle", "Normal", "Sensitive"];

    private readonly Button toggle;
    private readonly Label toggleCaption = Theme.Body("Put your earbuds in first", size: 13);
    private readonly Label state = Theme.Heading("Not running");
    private readonly Label detail = Theme.Body();
    private readonly Label problem = Theme.Body(color: Theme.Danger);
    private readonly VerticalStackLayout maskedList = new() { Spacing = 6 };
    private readonly Label volumeValue = Theme.Body(color: Theme.Text);
    private readonly Label boostValue = Theme.Body(color: Theme.Text);
    private readonly Button saveRecent = Theme.Secondary("Save the last 20 seconds");
    private readonly Border batteryCard;
    private readonly IDispatcherTimer timer;
    private NightSettings settings = NightSettings.Load();

    public NightPage()
    {
        Title = "Night";
        BackgroundColor = Theme.Background;
        toggle = new Button
        {
            WidthRequest = 188, HeightRequest = 188, CornerRadius = 94, FontFamily = Theme.Semibold, FontSize = 20,
            HorizontalOptions = LayoutOptions.Center, AutomationId = "night-toggle",
        };
        toggle.Clicked += async (_, _) => await ToggleAsync();
        saveRecent.Clicked += async (_, _) => await SaveRecentAsync();

        var volume = Theme.Slider(0, 1, settings.Volume);
        volume.ValueChanged += (_, e) => Change(settings with { Volume = (float)e.NewValue });
        var boost = Theme.Slider(12, 30, settings.MaxBoostDb);
        boost.ValueChanged += (_, e) => Change(settings with { MaxBoostDb = (float)Math.Round(e.NewValue) });
        var sensitivity = new ChoiceRow(Sensitivities, (int)settings.Sensitivity);
        sensitivity.Changed += i => Change(settings with { Sensitivity = (Sensitivity)i });

        var allow = Theme.Secondary("Allow running all night");
        allow.Clicked += (_, _) => AskForBatteryExemption();
        batteryCard = Theme.Card(
            Theme.Heading("Keep it running"),
            Theme.Body("Android may pause apps overnight to save battery. Let Quiet Night keep listening, and keep the phone on its charger."),
            allow);

        Content = Theme.Scroll(
            Theme.Title("Quiet Night"),
            Theme.Body("Masks cats, motorcycles and cars while you sleep. A soft noise plays in your earbuds and rises only when a disturbing sound needs covering."),
            new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, 12), Children = { toggle, toggleCaption } },
            Theme.Card(state, detail, problem, maskedList, saveRecent),
            Theme.Card(
                Row(Theme.Heading("Masker volume"), volumeValue), volume,
                Row(Theme.Heading("Strongest boost"), boostValue), boost,
                Theme.Body("How far the masker may rise above its bedtime level for a loud noise."),
                Theme.Heading("Reacts to"), sensitivity,
                Theme.Body("Gentle ignores small rises; Sensitive covers even faint noises.")),
            batteryCard,
            Theme.Body("Wired or Bluetooth earbuds both work. The masker never plays from the phone speaker: the microphone would hear it."));
        toggleCaption.HorizontalTextAlignment = TextAlignment.Center;
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += (_, _) => Render(NightSession.Status);
        ShowSettings();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        batteryCard.IsVisible = !IgnoringBatteryOptimizations();
        Render(NightSession.Status);
        timer.Start();
    }

    protected override void OnDisappearing()
    {
        timer.Stop();
        base.OnDisappearing();
    }

    private static Grid Row(View left, View right)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        grid.Add(left, 0);
        grid.Add(right, 1);
        right.VerticalOptions = LayoutOptions.Center;
        return grid;
    }

    private void Change(NightSettings next)
    {
        settings = next;
        settings.Save();
        NightSession.Engine?.SetBedtime(settings);
        ShowSettings();
    }

    private void ShowSettings()
    {
        volumeValue.Text = $"{settings.Volume:P0}";
        boostValue.Text = $"up to +{settings.MaxBoostDb:F0} dB";
    }

    private async Task ToggleAsync()
    {
        var context = Platform.AppContext;
        if (NightSession.Status.Running)
        {
            NightSession.Stop(context);
            return;
        }
        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
        {
            await DisplayAlertAsync("Microphone needed", "Quiet Night listens for noises with the microphone. Nothing is recorded or sent anywhere unless you save a recording.", "OK");
            return;
        }
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            await Permissions.RequestAsync<Permissions.PostNotifications>();
        if (!AudioRoutes.HeadphonesConnected(context))
        {
            await DisplayAlertAsync("Connect your earbuds", "Connect Bluetooth or wired earbuds or headphones first. Played from the speaker, the masker would be heard by the microphone and chase itself.", "OK");
            return;
        }
        NightSession.Start(context);
        Render(NightSession.Status with { Running = true, Problem = null });
    }

    private async Task SaveRecentAsync()
    {
        var labels = new Dictionary<string, NoiseClass>
        {
            ["Cat"] = NoiseClass.Cat, ["Motorcycle"] = NoiseClass.Motorcycle, ["Car"] = NoiseClass.Car, ["Not a disturbance"] = NoiseClass.Other,
        };
        var choice = await DisplayActionSheetAsync("What did you just hear?", "Cancel", null, [.. labels.Keys]);
        if (choice is null || !labels.TryGetValue(choice, out var label)) return;
        var saved = NightSession.SaveRecent(label);
        await DisplayAlertAsync(saved is null ? "Nothing to save" : "Saved",
            saved is null ? "Start the night first." : "Train on it from the Train tab to recognise it better next time.", "OK");
    }

    private void Render(NightStatus status)
    {
        toggle.Text = status.Running ? "Stop" : "Start";
        toggle.BackgroundColor = status.Running ? Theme.Raised : Theme.Accent;
        toggle.TextColor = status.Running ? Theme.Accent : Theme.Background;
        toggle.BorderColor = Theme.AccentDeep;
        toggle.BorderWidth = status.Running ? 2 : 0;
        toggleCaption.Text = status.Running ? "Listening. You can lock the phone." : "Put your earbuds in first";
        saveRecent.IsVisible = status.Running;
        problem.Text = status.Problem ?? "";
        problem.IsVisible = status.Problem is not null;
        if (!status.Running)
        {
            state.Text = "Not running";
            detail.Text = "Start the night when you are in bed.";
        }
        else if (status.LoudestBoostDb > 0 && status.Masking.IsTarget())
        {
            state.Text = $"Masking {Describe(status.Masking)}";
            detail.Text = $"+{status.LoudestBoostDb:F0} dB above your bedtime level";
        }
        else
        {
            state.Text = "Listening";
            var since = status.Since is { } at ? $"since {at.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}" : "";
            detail.Text = float.IsNaN(status.RoomDb) ? $"Measuring the room… {since}" : $"Room is steady {since}";
        }
        maskedList.Children.Clear();
        foreach (var noise in status.Masked.TakeLast(8).Reverse())
            maskedList.Children.Add(Theme.Body(
                $"{noise.At.ToLocalTime().ToString("t", CultureInfo.CurrentCulture)}  ·  {Capitalised(Describe(noise.Noise))}  ·  +{noise.LoudestBoostDb:F0} dB",
                Theme.Text));
        if (status.Running && status.Masked.Count == 0)
            maskedList.Children.Add(Theme.Body("Nothing masked yet tonight."));
    }

    private static string Describe(NoiseClass noise) => noise switch
    {
        NoiseClass.Cat => "a cat",
        NoiseClass.Motorcycle => "a motorcycle",
        NoiseClass.Car => "a car",
        _ => "a noise",
    };

    private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static bool IgnoringBatteryOptimizations()
    {
        var context = Platform.AppContext;
        var power = (PowerManager?)context.GetSystemService(Context.PowerService);
        return power?.IsIgnoringBatteryOptimizations(context.PackageName) ?? true;
    }

    private static void AskForBatteryExemption()
    {
        var context = Platform.AppContext;
        var intent = new Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations,
            Android.Net.Uri.Parse($"package:{context.PackageName}"));
        intent.AddFlags(ActivityFlags.NewTask);
        context.StartActivity(intent);
    }
}
