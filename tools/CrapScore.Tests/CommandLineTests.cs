namespace CrapScore.Tests;

public sealed class CommandLineTests
{
    [Fact]
    public void TryParseAcceptsMultipleCurrentAndBaseInputs()
    {
        var parsed = CommandLine.TryParse(
            ["current-one", "current-two.xml", "--base-reports", "base-one", "--base-reports", "base-two.xml", "--target", "5", "--output", "report.md"],
            out var options,
            out var error);

        Assert.True(parsed, error);
        Assert.Equal(["current-one", "current-two.xml"], options.ReportInputs);
        Assert.Equal(["base-one", "base-two.xml"], options.BaseReportInputs);
        Assert.Equal("report.md", options.OutputPath);
        Assert.Equal(5, options.Target);
    }

    [Theory]
    [InlineData()]
    [InlineData("--unknown")]
    [InlineData("reports", "--output")]
    [InlineData("reports", "--target", "nope")]
    [InlineData("reports", "--target", "5", "--target", "4")]
    [InlineData("reports", "--output", "one", "--output", "two")]
    public void TryParseRejectsInvalidArguments(params string[] arguments)
    {
        Assert.False(CommandLine.TryParse(arguments, out _, out var error));
        Assert.NotNull(error);
    }
}
