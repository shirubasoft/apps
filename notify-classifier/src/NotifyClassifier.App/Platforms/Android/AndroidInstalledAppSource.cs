using Android.Content.PM;

using NotifyClassifier.App.Services;
using NotifyClassifier.Core;

namespace NotifyClassifier.App.Platforms.Android;

using AndroidApplication = global::Android.App.Application;

public sealed class AndroidInstalledAppSource : IInstalledAppSource
{
    public Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<InstalledApp>>(
            () =>
            {
                var context = AndroidApplication.Context;
                var packageManager = context.PackageManager ??
                    throw new InvalidOperationException("Android PackageManager is unavailable.");
                var packages = packageManager.GetInstalledApplications(PackageInfoFlags.MatchAll);
                return packages
                    .Where(info => !string.Equals(info.PackageName, context.PackageName, StringComparison.Ordinal))
                    .Select(info => new InstalledApp(
                        info.PackageName ?? string.Empty,
                        packageManager.GetApplicationLabel(info)))
                    .Where(app => !string.IsNullOrWhiteSpace(app.PackageName))
                    .DistinctBy(app => app.PackageName, StringComparer.Ordinal)
                    .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(app => app.PackageName, StringComparer.Ordinal)
                    .ToArray();
            },
            cancellationToken);
}
