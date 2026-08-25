using Android.App.Job;
using Android.Content;

using NotifyClassifier.App.Services;

namespace NotifyClassifier.App.Platforms.Android;

using AndroidApplication = global::Android.App.Application;

public sealed class AndroidRetryScheduler : IRetryScheduler
{
    private const int PeriodicJobId = 41001;
    private const int ImmediateJobId = 41002;
    private static readonly long FifteenMinutesInMilliseconds = (long)TimeSpan.FromMinutes(15).TotalMilliseconds;

    public void SchedulePeriodic()
    {
        var scheduler = GetScheduler();
        using var builder = new JobInfo.Builder(PeriodicJobId, GetComponentName());
        builder.SetRequiredNetworkType(NetworkType.Any);
        builder.SetPersisted(true);
        builder.SetPeriodic(FifteenMinutesInMilliseconds);
        var job = builder.Build() ?? throw new InvalidOperationException("Could not create the periodic retry job.");
        scheduler.Schedule(job);
    }

    public void ScheduleSoon()
    {
        var scheduler = GetScheduler();
        using var builder = new JobInfo.Builder(ImmediateJobId, GetComponentName());
        builder.SetRequiredNetworkType(NetworkType.Any);
        builder.SetMinimumLatency(1_000);
        builder.SetOverrideDeadline(30_000);
        var job = builder.Build() ?? throw new InvalidOperationException("Could not create the immediate retry job.");
        scheduler.Schedule(job);
    }

    private static JobScheduler GetScheduler() =>
        (JobScheduler?)AndroidApplication.Context.GetSystemService(Context.JobSchedulerService) ??
        throw new InvalidOperationException("Android JobScheduler is unavailable.");

    private static ComponentName GetComponentName() => new(
        AndroidApplication.Context,
        Java.Lang.Class.FromType(typeof(RetryJobService)));
}
