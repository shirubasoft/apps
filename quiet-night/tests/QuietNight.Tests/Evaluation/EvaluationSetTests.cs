using QuietNight.Core.Detection;

namespace QuietNight.Tests.Evaluation;

/// <summary>
/// The frozen test split can build every pass the evaluation runs, so no category silently loses passes. Building a
/// scenario only mixes audio; no model runs on it here.
/// </summary>
public sealed class EvaluationSetTests
{
    [Theory]
    [InlineData("cat", 5)]
    [InlineData("car", 5)]
    [InlineData("motorcycle", 2)]
    public void TheTestSplitHoldsTheExpectedPasses(string category, int passes) =>
        Assert.Equal(passes, FivePassTests.PassesOf(category));

    [Fact]
    public void EveryPassBuildsWithItsTargetsAndProtectedSounds()
    {
        foreach (var category in NightScenario.Categories)
            for (var pass = 0; pass < FivePassTests.PassesOf(category); pass++)
            {
                var scenario = NightScenario.Build(category, pass);
                Assert.Equal(NightScenario.Targets, scenario.Events.Count(e => e.Noise.IsTarget()));
                Assert.Equal(NightScenario.Others, scenario.Events.Count(e => !e.Noise.IsTarget()));
            }
    }
}
