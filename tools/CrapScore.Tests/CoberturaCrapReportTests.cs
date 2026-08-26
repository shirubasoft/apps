using System.Xml.Linq;

namespace CrapScore.Tests;

public sealed class CoberturaCrapReportTests
{
    [Fact]
    public void MergeUnionsCoveredLinesAcrossReports()
    {
        var first = Parse(MethodXml("Assembly", "Source.cs", complexity: 4, firstHits: 1, secondHits: 0));
        var second = Parse(MethodXml("Assembly", "Source.cs", complexity: 4, firstHits: 0, secondHits: 2));

        var merged = Assert.Single(CoberturaCrapReport.Merge(first.Concat(second)));

        Assert.Equal(1, merged.LineCoverage);
        Assert.Equal(4, merged.Score);
    }

    [Fact]
    public void MergePreservesAssemblyAndSourceIdentity()
    {
        var first = Parse(MethodXml("First.Assembly", "First.cs", complexity: 2, firstHits: 1, secondHits: 1));
        var second = Parse(MethodXml("Second.Assembly", "Second.cs", complexity: 2, firstHits: 1, secondHits: 1));

        var merged = CoberturaCrapReport.Merge(first.Concat(second));

        Assert.Equal(2, merged.Count);
        Assert.Contains(merged, method => method.Identity.Assembly == "First.Assembly");
        Assert.Contains(merged, method => method.Identity.Assembly == "Second.Assembly");
    }

    [Fact]
    public void MergeRejectsConflictingComplexity()
    {
        var first = Parse(MethodXml("Assembly", "Source.cs", complexity: 2, firstHits: 1, secondHits: 1));
        var second = Parse(MethodXml("Assembly", "Source.cs", complexity: 3, firstHits: 1, secondHits: 1));

        var exception = Assert.Throws<InvalidDataException>(
            () => CoberturaCrapReport.Merge(first.Concat(second)));

        Assert.Contains("disagree on the complexity", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("complexity=\"\"")]
    [InlineData("complexity=\"not-a-number\"")]
    [InlineData("")]
    public void ParseRejectsMissingOrInvalidComplexity(string complexityAttribute)
    {
        var xml = MethodXml("Assembly", "Source.cs", complexity: 2, firstHits: 1, secondHits: 1)
            .Replace("complexity=\"2\"", complexityAttribute, StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Parse(xml));
    }

    [Fact]
    public void ParseRejectsMethodsWithoutLineData()
    {
        var xml = MethodXml("Assembly", "Source.cs", complexity: 2, firstHits: 1, secondHits: 1)
            .Replace(
                "<lines><line number=\"10\" hits=\"1\" /><line number=\"11\" hits=\"1\" /></lines>",
                "<lines />",
                StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Parse(xml));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-1")]
    public void ParseRejectsInvalidHitCounts(string hits)
    {
        var xml = MethodXml("Assembly", "Source.cs", complexity: 2, firstHits: 1, secondHits: 1)
            .Replace("hits=\"1\"", $"hits=\"{hits}\"", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Parse(xml));
    }

    private static IReadOnlyList<MethodCoverage> Parse(string xml) =>
        CoberturaCrapReport.Parse(XDocument.Parse(xml), "coverage.cobertura.xml");

    private static string MethodXml(
        string assembly,
        string source,
        int complexity,
        int firstHits,
        int secondHits) =>
        $$"""
        <coverage>
          <packages>
            <package name="{{assembly}}">
              <classes>
                <class name="Example.Type" filename="{{source}}">
                  <methods>
                    <method name="Run" signature="()" complexity="{{complexity}}">
                      <lines><line number="10" hits="{{firstHits}}" /><line number="11" hits="{{secondHits}}" /></lines>
                    </method>
                  </methods>
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;
}
