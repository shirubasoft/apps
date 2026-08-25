using Android.App;
using Android.App.Job;

using NotifyClassifier.App.Services;

namespace NotifyClassifier.App.Platforms.Android;

[Service(
    Name = "dev.danielreis.notifyclassifier.RetryJobService",
    Permission = "android.permission.BIND_JOB_SERVICE",
    Exported = true)]
public sealed class RetryJobService : JobService
{
    public override bool OnStartJob(JobParameters? @params)
    {
        if (@params is null)
        {
            return false;
        }

        _ = RunAsync(@params);
        return true;
    }

    public override bool OnStopJob(JobParameters? @params) => true;

    private async Task RunAsync(JobParameters parameters)
    {
        try
        {
            await AppRuntime.GetRequiredService<RetryCoordinator>().ProcessDueAsync();
        }
        finally
        {
            JobFinished(parameters, false);
        }
    }
}
