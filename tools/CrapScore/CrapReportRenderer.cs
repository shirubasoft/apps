using System.Globalization;
using System.Text;

namespace CrapScore;

internal static class CrapReportRenderer
{
    public static string Render(IReadOnlyList<MethodCoverage> methods, double target)
    {
        ArgumentNullException.ThrowIfNull(methods);
        if (methods.Count == 0)
        {
            throw new ArgumentException("At least one method is required.", nameof(methods));
        }

        var builder = new StringBuilder()
            .AppendLine("# CRAP score report")
            .AppendLine()
            .Append("Maximum method CRAP score: **")
            .Append(methods[0].Score.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine("**")
            .Append("Target maximum: **")
            .Append(target.ToString("F2", CultureInfo.InvariantCulture))
            .AppendLine("**")
            .Append("Methods measured: **")
            .Append(methods.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine("**")
            .AppendLine()
            .AppendLine("CRAP = complexity² × (1 − line coverage)³ + complexity.")
            .AppendLine()
            .AppendLine("| CRAP | Complexity | Coverage | Assembly | Source | Method |")
            .AppendLine("| ---: | ---: | ---: | --- | --- | --- |");

        foreach (var method in methods)
        {
            builder
                .Append("| ")
                .Append(method.Score.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(method.CyclomaticComplexity.ToString(CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(method.LineCoverage.ToString("P2", CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(Escape(method.Identity.Assembly))
                .Append(" | ")
                .Append(Escape(method.Identity.SourceFile))
                .Append(" | ")
                .Append(Escape(method.Identity.DisplayName))
                .AppendLine(" |");
        }

        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
