using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml.Linq;

namespace CrapScore;

internal sealed record MethodIdentity(
    string Assembly,
    string SourceFile,
    string Class,
    string Method,
    string Signature)
{
    public string DisplayName => $"{Class}.{Method}{Signature}";
}

internal sealed record MethodCoverage(
    MethodIdentity Identity,
    int CyclomaticComplexity,
    IReadOnlyDictionary<int, bool> Lines)
{
    public double LineCoverage => Lines.Count == 0
        ? throw new InvalidOperationException($"Method {Identity.DisplayName} has no line coverage data.")
        : (double)Lines.Count(line => line.Value) / Lines.Count;

    public double Score => CrapMetric.Calculate(CyclomaticComplexity, LineCoverage);
}

internal static class CoberturaCrapReport
{
    public static IReadOnlyList<MethodCoverage> Parse(XDocument document, string reportPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var methods = new List<MethodCoverage>();
        foreach (var package in Descendants(document, "package"))
        {
            var assembly = RequiredAttribute(package, "name", reportPath, "package");
            foreach (var classElement in Descendants(package, "class"))
            {
                var className = RequiredAttribute(classElement, "name", reportPath, "class");
                var sourceFile = NormalizePath(RequiredAttribute(classElement, "filename", reportPath, className));
                foreach (var methodElement in Descendants(classElement, "method"))
                {
                    methods.Add(ParseMethod(
                        methodElement,
                        new MethodIdentity(
                            assembly,
                            sourceFile,
                            className,
                            RequiredAttribute(methodElement, "name", reportPath, className),
                            RequiredAttribute(methodElement, "signature", reportPath, className)),
                        reportPath));
                }
            }
        }

        return methods;
    }

    public static IReadOnlyList<MethodCoverage> Merge(IEnumerable<MethodCoverage> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        var merged = new Dictionary<MethodIdentity, MethodCoverageBuilder>();
        foreach (var method in methods)
        {
            if (!merged.TryGetValue(method.Identity, out var builder))
            {
                builder = new MethodCoverageBuilder(method.Identity, method.CyclomaticComplexity);
                merged.Add(method.Identity, builder);
            }
            else if (builder.CyclomaticComplexity != method.CyclomaticComplexity)
            {
                throw new InvalidDataException(
                    $"Coverage reports disagree on the complexity of {method.Identity.DisplayName}: "
                    + $"{builder.CyclomaticComplexity} and {method.CyclomaticComplexity}.");
            }

            builder.AddLines(method.Lines);
        }

        if (merged.Count == 0)
        {
            throw new InvalidDataException("The Cobertura reports contain no methods with CRAP inputs.");
        }

        return merged.Values
            .Select(builder => builder.Build())
            .OrderByDescending(method => method.Score)
            .ThenBy(method => method.Identity.Assembly, StringComparer.Ordinal)
            .ThenBy(method => method.Identity.SourceFile, StringComparer.Ordinal)
            .ThenBy(method => method.Identity.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    private static MethodCoverage ParseMethod(
        XElement methodElement,
        MethodIdentity identity,
        string reportPath)
    {
        var complexityText = RequiredAttribute(methodElement, "complexity", reportPath, identity.DisplayName);
        if (!int.TryParse(complexityText, NumberStyles.None, CultureInfo.InvariantCulture, out var complexity)
            || complexity <= 0)
        {
            throw new InvalidDataException(
                $"Coverage report '{reportPath}' has invalid method complexity '{complexityText}' "
                + $"for {identity.DisplayName}.");
        }

        var lines = new Dictionary<int, bool>();
        foreach (var line in Descendants(methodElement, "line"))
        {
            var numberText = RequiredAttribute(line, "number", reportPath, identity.DisplayName);
            var hitsText = RequiredAttribute(line, "hits", reportPath, identity.DisplayName);
            if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                || number <= 0
                || !double.TryParse(hitsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var hits)
                || !double.IsFinite(hits)
                || hits < 0)
            {
                throw new InvalidDataException(
                    $"Coverage report '{reportPath}' has invalid line data for {identity.DisplayName}.");
            }

            lines[number] = lines.GetValueOrDefault(number) || hits > 0;
        }

        if (lines.Count == 0)
        {
            throw new InvalidDataException(
                $"Coverage report '{reportPath}' has no line data for {identity.DisplayName}.");
        }

        return new MethodCoverage(
            identity,
            complexity,
            new ReadOnlyDictionary<int, bool>(lines));
    }

    private static IEnumerable<XElement> Descendants(XContainer container, string localName) =>
        container.Descendants().Where(element => element.Name.LocalName == localName);

    private static string RequiredAttribute(
        XElement element,
        string localName,
        string reportPath,
        string subject)
    {
        var value = element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Coverage report '{reportPath}' is missing '{localName}' for {subject}.");
        }

        return value;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private sealed class MethodCoverageBuilder(MethodIdentity identity, int cyclomaticComplexity)
    {
        private readonly Dictionary<int, bool> _lines = [];

        public int CyclomaticComplexity { get; } = cyclomaticComplexity;

        public void AddLines(IReadOnlyDictionary<int, bool> lines)
        {
            foreach (var line in lines)
            {
                _lines[line.Key] = _lines.GetValueOrDefault(line.Key) || line.Value;
            }
        }

        public MethodCoverage Build() => new(
            identity,
            CyclomaticComplexity,
            new ReadOnlyDictionary<int, bool>(_lines));
    }
}
