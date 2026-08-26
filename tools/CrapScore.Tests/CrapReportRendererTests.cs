using System.Collections.ObjectModel;

namespace CrapScore.Tests;

public sealed class CrapReportRendererTests
{
    [Fact]
    public void RenderIncludesEveryMethodAndEscapesMarkdownPipes()
    {
        var methods = Enumerable.Range(1, 51)
            .Select(index => new MethodCoverage(
                new MethodIdentity("Assembly", "Source|File.cs", "Example.Type", $"Method{index}", "()"),
                1,
                new ReadOnlyDictionary<int, bool>(new Dictionary<int, bool> { [index] = true })))
            .ToArray();

        var report = CrapReportRenderer.Render(methods, CrapGate.DefaultTarget);

        Assert.Contains("Methods measured: **51**", report, StringComparison.Ordinal);
        Assert.Contains("Method51()", report, StringComparison.Ordinal);
        Assert.Contains("Source\\|File.cs", report, StringComparison.Ordinal);
    }
}
