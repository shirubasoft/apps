namespace CrapScore.Tests;

public sealed class CrapGateTests
{
    [Theory]
    [InlineData(21, 22, 5, true)]
    [InlineData(22, 22, 5, false)]
    [InlineData(23, 22, 5, false)]
    [InlineData(5, 5, 5, true)]
    [InlineData(4, 5, 5, true)]
    [InlineData(5.000001, 5, 5, false)]
    public void EvaluateRatchetsTowardTarget(
        double current,
        double baseline,
        double target,
        bool expected)
    {
        Assert.Equal(expected, CrapGate.Evaluate(current, baseline, target).Passed);
    }

    [Fact]
    public void EvaluateComparesCanonicalScores()
    {
        var result = CrapGate.Evaluate(10.0000004, 10.00000049, CrapGate.DefaultTarget);

        Assert.False(result.Passed);
    }
}
