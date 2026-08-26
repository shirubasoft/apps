namespace CrapScore;

internal static class CrapMetric
{
    public static double Calculate(int cyclomaticComplexity, double lineCoverage)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cyclomaticComplexity);
        if (!double.IsFinite(lineCoverage) || lineCoverage is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lineCoverage),
                lineCoverage,
                "Line coverage must be a finite value between 0 and 1.");
        }

        var complexity = (double)cyclomaticComplexity;
        return (complexity * complexity * Math.Pow(1 - lineCoverage, 3)) + complexity;
    }
}
