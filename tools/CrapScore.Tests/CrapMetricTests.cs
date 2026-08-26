namespace CrapScore.Tests;

public sealed class CrapMetricTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1, 0, 2)]
    [InlineData(2, 0.5, 2.5)]
    [InlineData(10, 0.8, 10.8)]
    public void CalculateUsesLineCoverage(
        int complexity,
        double coverage,
        double expected)
    {
        Assert.Equal(expected, CrapMetric.Calculate(complexity, coverage), precision: 10);
    }

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(1, -0.01)]
    [InlineData(1, 1.01)]
    [InlineData(1, double.NaN)]
    [InlineData(1, double.PositiveInfinity)]
    public void CalculateRejectsInvalidInputs(int complexity, double coverage)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CrapMetric.Calculate(complexity, coverage));
    }
}
