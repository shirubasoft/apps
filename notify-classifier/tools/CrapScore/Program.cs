using System.Globalization;
using System.Text;
using System.Xml.Linq;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: CrapScore <coverage-directory> [--threshold <score>] [--output <path>]");
    return 2;
}

var coverageDirectory = Path.GetFullPath(args[0]);
var threshold = GetOption(args, "--threshold") is { } value
    ? double.Parse(value, CultureInfo.InvariantCulture)
    : 30d;
var outputPath = GetOption(args, "--output");
var reports = Directory.Exists(coverageDirectory)
    ? Directory.GetFiles(coverageDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories)
    : [];

if (reports.Length == 0)
{
    Console.Error.WriteLine($"No Cobertura reports found under {coverageDirectory}.");
    return 2;
}

var methods = reports.SelectMany(ReadMethods)
    .GroupBy(method => method.Key, StringComparer.Ordinal)
    .Select(group => group.OrderBy(method => method.CrapScore).First())
    .OrderByDescending(method => method.CrapScore)
    .ThenBy(method => method.Key, StringComparer.Ordinal)
    .ToArray();
var offenders = methods.Where(method => method.CrapScore > threshold).ToArray();
var report = BuildReport(methods, threshold);

Console.WriteLine(report);
if (!string.IsNullOrWhiteSpace(outputPath))
{
    var fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
    File.WriteAllText(fullOutputPath, report);
}

return offenders.Length == 0 ? 0 : 1;

static string? GetOption(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static IEnumerable<MethodScore> ReadMethods(string reportPath)
{
    var document = XDocument.Load(reportPath, LoadOptions.None);
    foreach (var classElement in document.Descendants("class"))
    {
        var className = classElement.Attribute("name")?.Value ?? "unknown";
        foreach (var methodElement in classElement.Descendants("method"))
        {
            var methodName = methodElement.Attribute("name")?.Value ?? "unknown";
            var signature = methodElement.Attribute("signature")?.Value ?? string.Empty;
            var complexity = ParseDouble(methodElement.Attribute("complexity")?.Value, 1d);
            var lines = methodElement.Descendants("line").ToArray();
            var coveredLines = lines.Count(line => ParseDouble(line.Attribute("hits")?.Value, 0d) > 0);
            var coverage = lines.Length == 0 ? 1d : (double)coveredLines / lines.Length;
            var crap = (complexity * complexity * Math.Pow(1d - coverage, 3d)) + complexity;
            yield return new MethodScore($"{className}.{methodName}{signature}", complexity, coverage, crap);
        }
    }
}

static double ParseDouble(string? value, double fallback) =>
    double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

static string BuildReport(IReadOnlyList<MethodScore> methods, double threshold)
{
    var builder = new StringBuilder();
    builder.AppendLine("# CRAP score report");
    builder.AppendLine();
    builder.AppendLine(CultureInfo.InvariantCulture, $"Threshold: {threshold:F2}");
    builder.AppendLine();
    builder.AppendLine("| Method | Complexity | Coverage | CRAP | Result |");
    builder.AppendLine("| --- | ---: | ---: | ---: | --- |");
    foreach (var method in methods.Take(50))
    {
        var result = method.CrapScore <= threshold ? "pass" : "fail";
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"| `{method.Key}` | {method.Complexity:F0} | {method.Coverage:P0} | {method.CrapScore:F2} | {result} |");
    }

    return builder.ToString();
}

internal sealed record MethodScore(string Key, double Complexity, double Coverage, double CrapScore);
