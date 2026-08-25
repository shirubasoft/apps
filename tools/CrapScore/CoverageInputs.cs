using System.Xml.Linq;

namespace CrapScore;

internal static class CoverageInputs
{
    public static async Task<IReadOnlyList<MethodCoverage>> ReadAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var reportPaths = Resolve(inputs);
        var methods = new List<MethodCoverage>();
        foreach (var reportPath in reportPaths)
        {
            await using var stream = File.OpenRead(reportPath);
            var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
            methods.AddRange(CoberturaCrapReport.Parse(document, reportPath));
        }

        return CoberturaCrapReport.Merge(methods);
    }

    internal static IReadOnlyList<string> Resolve(IReadOnlyList<string> inputs)
    {
        if (inputs.Count == 0)
        {
            throw new ArgumentException("At least one coverage file or directory is required.", nameof(inputs));
        }

        var reports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            var fullPath = Path.GetFullPath(input);
            if (File.Exists(fullPath))
            {
                reports.Add(fullPath);
                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                throw new FileNotFoundException($"Coverage input does not exist: {input}", fullPath);
            }

            reports.UnionWith(Directory.EnumerateFiles(
                fullPath,
                "coverage.cobertura.xml",
                SearchOption.AllDirectories));
        }

        if (reports.Count == 0)
        {
            throw new FileNotFoundException(
                "No coverage.cobertura.xml reports were found in the supplied inputs.");
        }

        return reports.Order(StringComparer.Ordinal).ToArray();
    }
}
