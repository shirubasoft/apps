namespace NotifyClassifier.Core;

public static class RetryPolicy
{
    private static readonly TimeSpan MaximumDelay = TimeSpan.FromHours(6);

    public static TimeSpan GetDelay(int failedAttemptCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttemptCount, 1);

        var exponent = Math.Min(failedAttemptCount - 1, 10);
        var seconds = 30 * Math.Pow(2, exponent);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaximumDelay.TotalSeconds));
    }
}
