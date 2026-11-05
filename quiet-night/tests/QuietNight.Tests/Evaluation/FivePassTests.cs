using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using QuietNight.Core.Detection;
using QuietNight.Tests.Support;
using Xunit.Abstractions;

namespace QuietNight.Tests.Evaluation;

/// <summary>Which target noises the user masks, and so which categories' passes the configuration is judged on.</summary>
public sealed record MaskingChoice(string Name, IgnoredNoises Ignored, IReadOnlyList<string> Categories)
{
    public static readonly MaskingChoice[] All =
    [
        new("All three", default, NightScenario.Categories),
        new("Cars switched off", new IgnoredNoises(Cars: true), ["cat", "motorcycle"]),
        new("Cats only", new IgnoredNoises(Motorcycles: true, Cars: true), ["cat"]),
    ];
}

/// <summary>Collects every pass and writes the Markdown report once the evaluation finishes.</summary>
public sealed class EvaluationReport : IDisposable
{
    public ConcurrentBag<(MaskingChoice Choice, PassResult Result)> Results { get; } = [];

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
        text.AppendLine("# Five-pass masking evaluation").AppendLine();
        text.AppendLine("Coverage is controller coverage: it assumes the bedtime masker, as the user sets its volume, just covers the room's usual peaks. The oracle has the same masker, delay, boost limit and release, but knows when a target is present and how loud the room is; what it misses, no causal controller could cover.").AppendLine();
        foreach (var choice in MaskingChoice.All)
        {
            var results = Results.Where(r => r.Choice == choice).Select(r => r.Result)
                .OrderBy(r => Array.IndexOf(NightScenario.Categories, r.Scenario.Category)).ThenBy(r => r.Scenario.Pass).ToList();
            if (results.Count == 0) continue;
            text.AppendLine(culture, $"## {choice.Name}").AppendLine();
            text.AppendLine(culture, $"{results.Count(r => r.Passed)} of {results.Count} category passes succeeded under the criteria in `PassResult` (version 3). For comparison, {results.Count(r => r.PassedVersion2)} would pass version 2 and {results.Count(r => r.PassedVersion1)} version 1.").AppendLine();
            text.AppendLine("| Category | Pass | Result (v2, v1) | Recognised | Median reaction | Coverage at once / oracle (worst gap) | Via 250 ms Bluetooth / oracle | Longest protected boost | Quiet night boosted | Whole night boosted | Mean release | Listening path per hop | Sound model call |");
            text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var r in results)
                text.AppendLine(culture,
                    $"| {r.Scenario.Category} | {r.Scenario.Pass + 1} | {(r.Passed ? "pass" : "FAIL")} ({(r.PassedVersion2 ? "pass" : "fail")}, {(r.PassedVersion1 ? "pass" : "fail")}) | {r.CorrectlyRecognised}/{r.Recognisable.Count} | {r.MedianReactionMilliseconds:F0} ms | {r.WholeAtOnce:P1} / {r.OracleAtOnce:P1} ({r.WorstEventOracleGap:P0}) | {r.WholeThroughBluetooth:P1} / {r.OracleThroughBluetooth:P1} | {r.LongestProtectedBoostSeconds:F1} s | {r.NightQuietBoostedShare:P1} | {r.NightBoostedShare:P1} | {r.MeanReleaseSeconds:F1} s | {r.HopMeanMilliseconds:F3} ms | {r.VerdictMedianMilliseconds:F1} ms |");
            text.AppendLine().AppendLine(culture, $"### Events: {choice.Name.ToLowerInvariant()}").AppendLine();
            text.AppendLine("| Category | Pass | Freesound clip | Class | Level above room | Needing hops | At once / oracle | Via Bluetooth / oracle | Reaction | Recognised as | Boosted | Peak boost | Release |");
            text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var r in results)
                foreach (var e in r.Targets.Concat(r.Others).OrderBy(e => e.Event.StartHop))
                    text.AppendLine(culture,
                        $"| {r.Scenario.Category} | {r.Scenario.Pass + 1} | [{e.Event.SoundId}](https://freesound.org/s/{e.Event.SoundId}/) | {e.Event.Noise} | {e.Event.LevelDb:F1} dB | {e.NeedingHops} | {e.Whole.AppShare:P0} / {e.Whole.OracleShare:P0} | {e.Whole.AppLateShare:P0} / {e.Whole.OracleLateShare:P0} | {(e.ReactionMilliseconds is { } ms ? $"{ms:F0} ms" : "—")} | {(e.Recognised?.ToString() ?? "—")} ({e.Confirmations}) | {e.BoostedSeconds:F1} s | {e.PeakBoostDb:F0} dB | {e.ReleaseSeconds:F1} s |");
            text.AppendLine();
        }
        return text.ToString();
    }
}

/// <summary>
/// Five independent passes per category over held-out reference sounds, or as many as the test split allows. Each pass mixes four cat, motorcycle or car
/// recordings and two sounds that must not trigger masking into a held-out room, runs the app's exact listening path
/// with the bundled YAMNet model and heads, and judges it by the criteria in <see cref="PassResult"/>, for each choice of
/// noises to mask. The verdicts are
/// the evaluation's measured result, published in its report with every release; the test checks that every pass was
/// measured.
/// </summary>
public sealed class FivePassTests(EvaluationReport report, ITestOutputHelper output) : IClassFixture<EvaluationReport>
{
    public const int Passes = 5;

    /// <summary>Five passes, or as many as the test split holds unseen recordings of the category for.</summary>
    public static int PassesOf(string category) =>
        Math.Min(Passes, TestAssets.Manifest.Count(s => s.IsTest && s.Group == category) / NightScenario.Targets);

    public static TheoryData<string, string, int> Cases()
    {
        var cases = new TheoryData<string, string, int>();
        foreach (var choice in MaskingChoice.All)
            foreach (var category in choice.Categories)
                for (var pass = 0; pass < PassesOf(category); pass++)
                    cases.Add(choice.Name, category, pass);
        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void MasksTheCategoryQuicklyAndOnlyWhenNeeded(string masking, string category, int pass)
    {
        var choice = MaskingChoice.All.Single(c => c.Name == masking);
        var settings = ListenerSettings.Default with { Ignored = choice.Ignored };
        var scenario = NightScenario.Build(category, pass);
        using var model = TestAssets.CreateSoundModel();
        var trace = NightTrace.Record(scenario, TestAssets.Heads(), model, settings);
        var result = PassResult.Of(scenario, trace, settings.Masker);
        report.Results.Add((choice, result));
        output.WriteLine($"{choice.Name}: {result.Summary}");
        Assert.Equal(NightScenario.Targets, result.Targets.Count);
        Assert.Equal(NightScenario.Others, result.Others.Count);
    }
}
