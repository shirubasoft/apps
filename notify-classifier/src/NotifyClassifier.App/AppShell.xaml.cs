using NotifyClassifier.App.Pages;

namespace NotifyClassifier.App;

public sealed class AppShell : Shell
{
    public AppShell(
        DashboardPage dashboardPage,
        AppsPage appsPage,
        SchemasPage schemasPage,
        QueuePage queuePage)
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        var tabs = new TabBar();
        tabs.Items.Add(CreateTab("Monitor", dashboardPage));
        tabs.Items.Add(CreateTab("Apps", appsPage));
        tabs.Items.Add(CreateTab("Schemas", schemasPage));
        tabs.Items.Add(CreateTab("Queue", queuePage));
        Items.Add(tabs);
    }

    private static ShellContent CreateTab(string title, Page page) => new()
    {
        Title = title,
        Content = page
    };
}
