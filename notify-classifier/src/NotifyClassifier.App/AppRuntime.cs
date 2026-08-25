namespace NotifyClassifier.App;

public static class AppRuntime
{
    private static IServiceProvider? s_services;

    public static IServiceProvider Services
    {
        get => s_services ?? throw new InvalidOperationException("The app services are not ready.");
        internal set => s_services = value;
    }

    public static T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();
}
