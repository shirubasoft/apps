using System.Globalization;

namespace CrapScore;

internal sealed record CrapGateResult(bool Passed, string Message);

internal static class CrapGate
{
    public const double DefaultTarget = 5;

    public static CrapGateResult Evaluate(double currentMaximum, double baseMaximum, double target)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(target, 0);

        currentMaximum = Canonicalize(currentMaximum);
        baseMaximum = Canonicalize(baseMaximum);
        target = Canonicalize(target);

        if (baseMaximum <= target)
        {
            return currentMaximum <= target
                ? new CrapGateResult(
                    true,
                    $"CRAP maximum is {Format(currentMaximum)}, at or below the target of {Format(target)}.")
                : new CrapGateResult(
                    false,
                    $"CRAP maximum {Format(currentMaximum)} exceeds the target of {Format(target)}; "
                    + $"the main-branch maximum is {Format(baseMaximum)}.");
        }

        return currentMaximum < baseMaximum
            ? new CrapGateResult(
                true,
                $"CRAP maximum reduced from {Format(baseMaximum)} on the main branch to {Format(currentMaximum)}; "
                + $"the target is {Format(target)}.")
            : new CrapGateResult(
                false,
                $"CRAP reduction required: current maximum {Format(currentMaximum)} must be lower than "
                + $"the main-branch maximum {Format(baseMaximum)}; the target is {Format(target)}.");
    }

    internal static double Canonicalize(double value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private static string Format(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
}
