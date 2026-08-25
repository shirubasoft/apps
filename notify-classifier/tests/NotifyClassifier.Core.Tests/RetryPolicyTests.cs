namespace NotifyClassifier.Core.Tests;

public sealed class RetryPolicyTests
{
    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(6, 960)]
    [InlineData(20, 21600)]
    public void GetDelayUsesExponentialBackoffWithSixHourCap(int attempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetryPolicy.GetDelay(attempt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetDelayRejectsNonPositiveAttempts(int attempt)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetryPolicy.GetDelay(attempt));
    }
}
