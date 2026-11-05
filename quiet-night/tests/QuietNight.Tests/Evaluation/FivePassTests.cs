using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using QuietNight.Core.Detection;
using QuietNight.Tests.Support;
using Xunit.Abstractions;

namespace QuietNight.Tests.Evaluation;

/// <summary>Collects every pass and writes the Markdown report once the evaluation finishes.</summary>
public sealed class EvaluationReport : IDisposable
{
    public ConcurrentBag<PassResult> Results { get; } = [];

    public void Dispose()
    {
        if (Results.IsEmpty) return;
        var directory = Environment.GetEnvironmentVariable("QUIET_NIGHT_REPORT_DIR") is { Length: > 0 } configured
            ? configured
            : Path.Combine(AppContext.BaseDirectory, "evaluation");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "five-pass-evaluation.md"), Markdown(), Encoding.UTF8);
    }

    private string Markdown()
    {
        var text = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        var results = Results.OrderBy(r => Array.IndexOf(NightScenario.Categories, r.Scenario.Category)).ThenBy(r => r.Scenario.Pass).ToList();
        text.AppendLine("# Five-pass masking evaluation").AppendLine();
        text.AppendLine(culture, $"{results.Count(r => r.Passed)} of {results.Count} category passes succeeded.").AppendLine();
        text.AppendLine("| Category | Pass | Result | Recognised | Median reaction | Coverage at once | Coverage via 250 ms Bluetooth | Others boosted after 1 s (overall) | Quiet boosted under the cover (overall) | Listening path per hop | Sound model call |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var r in results)
            text.AppendLine(culture,
                $"| {r.Scenario.Category} | {r.Scenario.Pass + 1} | {(r.Passed ? "pass" : "FAIL")} | {r.CorrectlyRecognised}/{r.Recognisable.Count} | {r.MedianReactionMilliseconds:F0} ms | {r.CoverageAtOnce:P1} | {r.CoverageThroughBluetooth:P1} | {r.OtherBoostedAfterFirstSecond:P0} ({r.OtherBoostedShare:P0}) | {r.CoveredQuietBoostedShare:P1} ({r.QuietBoostedShare:P1}) | {r.HopMeanMilliseconds:F3} ms | {r.VerdictMedianMilliseconds:F1} ms |");
        text.AppendLine().AppendLine("## Events").AppendLine();
        text.AppendLine("| Category | Pass | Freesound clip | Class | Level above room | Needing hops | Masked at once | Masked via Bluetooth | Reaction | Recognised as | Boosted |");
        text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var r in results)
            foreach (var e in r.Targets.Concat(r.Others).OrderBy(e => e.Event.StartHop))
                text.AppendLine(culture,
                    $"| {r.Scenario.Category} | {r.Scenario.Pass + 1} | [{e.Event.SoundId}](https://freesound.org/s/{e.Event.SoundId}/) | {e.Event.Noise} | {e.Event.LevelDb:F1} dB | {e.NeedingHops} | {Share(e.MaskedAtOnce, e.NeedingHops)} | {Share(e.MaskedThroughBluetooth, e.NeedingHops)} | {(e.ReactionMilliseconds is { } ms ? $"{ms:F0} ms" : "—")} | {(e.Recognised?.ToString() ?? "—")} ({e.Confirmations}) | {e.BoostedShare:P0} |");
        return text.ToString();

        string Share(int part, int whole) => whole == 0 ? "—" : ((double)part / whole).ToString("P1", culture);
    }
}

/// <summary>
/// Five independent passes per category over held-out reference sounds. Each pass mixes four cat, motorcycle or car
/// recordings and two sounds that must not trigger masking into a held-out room, runs the app's exact listening path
/// with the bundled YAMNet model and heads, and checks the criteria in <see cref="PassResult"/>.
/// </summary>
public sealed class FivePassTests(EvaluationReport report, ITestOutputHelper output) : IClassFixture<EvaluationReport>
{
    public const int Passes = 5;

    public static TheoryData<string, int> Cases()
    {
        var cases = new TheoryData<string, int>();
        foreach (var category in NightScenario.Categories)
            for (var pass = 0; pass < Passes; pass++)
                cases.Add(category, pass);
        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void MasksTheCategoryQuicklyAndOnlyWhenNeeded(string category, int pass)
    {
        var settings = ListenerSettings.Default;
        var scenario = NightScenario.Build(category, pass);
        using var model = TestAssets.CreateSoundModel();
        var trace = NightTrace.Record(scenario, TestAssets.Heads(), model, settings);
        var result = PassResult.Of(scenario, trace, settings.Masker);
        report.Results.Add(result);
        output.WriteLine(result.Summary);
        Assert.True(result.Passed, result.Summary);
    }
}
