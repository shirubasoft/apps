using Microsoft.Extensions.Logging;

using NotifyClassifier.App.Pages;
using NotifyClassifier.App.Platforms.Android;
using NotifyClassifier.App.Services;
using NotifyClassifier.Storage;

namespace NotifyClassifier.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "notify-classifier.db3");
        builder.Services.AddSingleton<INotificationRepository>(_ => new NotificationRepository(databasePath));
        builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(30) });
        builder.Services.AddSingleton<ApiEndpointSettings>();
        builder.Services.AddSingleton<ClassifierApiClient>();
        builder.Services.AddSingleton<RetryCoordinator>();
        builder.Services.AddSingleton<IInstalledAppSource, AndroidInstalledAppSource>();
        builder.Services.AddSingleton<INotificationAccess, AndroidNotificationAccess>();
        builder.Services.AddSingleton<IRetryScheduler, AndroidRetryScheduler>();
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<AppsPage>();
        builder.Services.AddSingleton<SchemasPage>();
        builder.Services.AddSingleton<QueuePage>();
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        AppRuntime.Services = app.Services;
        return app;
    }
}
