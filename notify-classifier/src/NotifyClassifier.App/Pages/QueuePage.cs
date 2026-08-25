using NotifyClassifier.Storage;

namespace NotifyClassifier.App.Pages;

public sealed class QueuePage(INotificationRepository repository) : ContentPage
{
    private readonly VerticalStackLayout _rows = new() { Spacing = 6 };

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Content is not null)
        {
            return;
        }

        Title = "Queue";
        var content = PageStyles.Content();
        content.Children.Add(PageStyles.Title("Notification history"));
        content.Children.Add(PageStyles.Subtitle("Completed items stay here. Failed items keep their next retry time and error."));
        content.Children.Add(_rows);
        Content = new ScrollView { Content = content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await repository.InitializeAsync();
        var notifications = await repository.GetHistoryAsync(100);
        _rows.Children.Clear();
        foreach (var notification in notifications)
        {
            _rows.Children.Add(CreateRow(notification));
        }

        if (notifications.Count == 0)
        {
            _rows.Children.Add(PageStyles.Subtitle("No captured notifications yet."));
        }
    }

    private static VerticalStackLayout CreateRow(StoredNotification notification)
    {
        var layout = new VerticalStackLayout
        {
            Spacing = 2,
            Padding = new Thickness(4, 8),
            AutomationId = $"QueueItem_{notification.Id}"
        };
        layout.Children.Add(new Label
        {
            Text = $"{notification.AppName}: {notification.Title ?? "Notification"}",
            FontAttributes = FontAttributes.Bold
        });
        layout.Children.Add(new Label
        {
            Text = $"{notification.State} • {notification.SchemaName} • attempts {notification.AttemptCount}",
            FontSize = 12
        });
        if (!string.IsNullOrWhiteSpace(notification.ResultJson))
        {
            layout.Children.Add(new Label { Text = notification.ResultJson, FontSize = 12 });
        }
        else if (!string.IsNullOrWhiteSpace(notification.LastError))
        {
            layout.Children.Add(new Label
            {
                Text = $"Retry after {notification.NextAttemptAt.LocalDateTime:g}: {notification.LastError}",
                FontSize = 12,
                TextColor = Color.FromArgb("#A1362D")
            });
        }

        layout.Children.Add(PageStyles.Divider());
        return layout;
    }
}
