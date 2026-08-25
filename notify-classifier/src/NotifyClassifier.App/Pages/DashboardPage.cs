using NotifyClassifier.App.Services;
using NotifyClassifier.Storage;

namespace NotifyClassifier.App.Pages;

public sealed class DashboardPage : ContentPage
{
    private readonly INotificationRepository _repository;
    private readonly ApiEndpointSettings _settings;
    private readonly RetryCoordinator _retryCoordinator;
    private readonly INotificationAccess _notificationAccess;
    private readonly IRetryScheduler _retryScheduler;
    private readonly Entry _apiUrl;
    private readonly Label _accessStatus;
    private readonly Label _queueStatus;
    private readonly Label _runStatus;

    public DashboardPage(
        INotificationRepository repository,
        ApiEndpointSettings settings,
        RetryCoordinator retryCoordinator,
        INotificationAccess notificationAccess,
        IRetryScheduler retryScheduler)
    {
        _repository = repository;
        _settings = settings;
        _retryCoordinator = retryCoordinator;
        _notificationAccess = notificationAccess;
        _retryScheduler = retryScheduler;
        Title = "Monitor";

        _apiUrl = new Entry
        {
            Text = settings.Url,
            Keyboard = Keyboard.Url,
            AutomationId = "ApiUrlEntry",
            Placeholder = "http://10.0.2.2:5080"
        };
        _accessStatus = new Label { AutomationId = "NotificationAccessStatus" };
        _queueStatus = new Label { AutomationId = "QueueStatus", FontAttributes = FontAttributes.Bold };
        _runStatus = new Label { AutomationId = "RetryRunStatus", TextColor = Color.FromArgb("#566473") };

        var saveUrl = PageStyles.PrimaryButton("Save API address", "SaveApiUrlButton");
        saveUrl.Clicked += SaveUrlClicked;
        var access = PageStyles.PrimaryButton("Open notification access", "NotificationAccessButton");
        access.Clicked += (_, _) => _notificationAccess.OpenSettings();
        var retry = PageStyles.PrimaryButton("Retry queued notifications", "RetryNowButton");
        retry.Clicked += RetryClicked;

        var content = PageStyles.Content();
        content.Children.Add(PageStyles.Title("Notify Classifier"));
        content.Children.Add(PageStyles.Subtitle(
            "Selected apps are classified with GPT-5.6 Luna. The phone keeps every notification and retries outages automatically."));
        content.Children.Add(PageStyles.Divider());
        content.Children.Add(new Label { Text = "Classifier API", FontAttributes = FontAttributes.Bold });
        content.Children.Add(_apiUrl);
        content.Children.Add(saveUrl);
        content.Children.Add(PageStyles.Divider());
        content.Children.Add(new Label { Text = "Notification listener", FontAttributes = FontAttributes.Bold });
        content.Children.Add(_accessStatus);
        content.Children.Add(access);
        content.Children.Add(PageStyles.Divider());
        content.Children.Add(new Label { Text = "Durable queue", FontAttributes = FontAttributes.Bold });
        content.Children.Add(_queueStatus);
        content.Children.Add(retry);
        content.Children.Add(_runStatus);
        Content = new ScrollView { Content = content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _retryScheduler.SchedulePeriodic();
        await RefreshAsync();
    }

    private async void SaveUrlClicked(object? sender, EventArgs eventArgs)
    {
        try
        {
            _settings.Url = _apiUrl.Text ?? string.Empty;
            _apiUrl.Text = _settings.Url;
            await DisplayAlertAsync("Saved", "New classifications will use this API address.", "OK");
        }
        catch (ArgumentException exception)
        {
            await DisplayAlertAsync("Invalid address", exception.Message, "OK");
        }
    }

    private async void RetryClicked(object? sender, EventArgs eventArgs)
    {
        _runStatus.Text = "Retrying...";
        var result = await _retryCoordinator.ProcessDueAsync();
        _runStatus.Text = $"Processed {result.Processed}, completed {result.Completed}, waiting {result.Failed}.";
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await _repository.InitializeAsync();
        var summary = await _repository.GetSummaryAsync();
        _accessStatus.Text = _notificationAccess.HasAccess ? "Access granted" : "Access required";
        _queueStatus.Text = $"{summary.Waiting} waiting, {summary.Completed} completed and retained";
    }
}
