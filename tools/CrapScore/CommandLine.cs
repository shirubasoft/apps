using System.Globalization;

namespace CrapScore;

internal sealed record CommandLineOptions(
    IReadOnlyList<string> ReportInputs,
    IReadOnlyList<string> BaseReportInputs,
    string? OutputPath,
    double Target);

internal static class CommandLine
{
    public const string Usage =
        "Usage: CrapScore <coverage-file-or-directory>... [--output <markdown-file>] "
        + "[--base-reports <coverage-file-or-directory>]... [--target <score>]\n"
        + "       CrapScore download-artifact <owner/repository> <commit> <output-directory>";

    public static bool TryParse(string[] args, out CommandLineOptions options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        var reports = new List<string>();
        var baseReports = new List<string>();
        string? outputPath = null;
        var target = CrapGate.DefaultTarget;
        var targetSet = false;
        error = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--output" when outputPath is null && TryReadValue(args, ref index, out var value):
                    outputPath = value;
                    break;
                case "--base-reports" when TryReadValue(args, ref index, out var value):
                    baseReports.Add(value);
                    break;
                case "--target" when !targetSet
                    && TryReadValue(args, ref index, out var value)
                    && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    && double.IsFinite(parsed)
                    && parsed >= 0:
                    target = parsed;
                    targetSet = true;
                    break;
                case var argument when argument.StartsWith("--", StringComparison.Ordinal):
                    error = $"Unknown, duplicated, or incomplete option: {argument}";
                    options = EmptyOptions();
                    return false;
                case var input:
                    reports.Add(input);
                    break;
            }
        }

        if (reports.Count == 0)
        {
            error = "At least one coverage file or directory is required.";
            options = EmptyOptions();
            return false;
        }

        options = new CommandLineOptions(reports, baseReports, outputPath, target);
        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, out string value)
    {
        index++;
        if (index >= args.Length
            || string.IsNullOrWhiteSpace(args[index])
            || args[index].StartsWith("--", StringComparison.Ordinal))
        {
            value = string.Empty;
            return false;
        }

        value = args[index];
        return true;
    }

    private static CommandLineOptions EmptyOptions() => new([], [], null, CrapGate.DefaultTarget);
}
