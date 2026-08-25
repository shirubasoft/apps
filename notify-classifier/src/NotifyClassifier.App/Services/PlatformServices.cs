using NotifyClassifier.Core;

namespace NotifyClassifier.App.Services;

public interface IInstalledAppSource
{
    Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken = default);
}

public interface INotificationAccess
{
    bool HasAccess { get; }

    void OpenSettings();
}

public interface IRetryScheduler
{
    void SchedulePeriodic();

    void ScheduleSoon();
}
