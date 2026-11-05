using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;

namespace QuietNight.Tests.Evaluation;

/// <summary>The acceptance criteria applied to hand-made nights, so each rule is checked on its own.</summary>
public sealed class ScoringTests
{
    private const int Hops = 4_000;
    private const float RoomPower = 1e-6f;
    private const float SoundPower = 1e-3f;
    private static readonly int Settle = ListeningFormat.Hops(NightScenario.Settle);

    [Fact]
    public void QuietTimeCountsOnceTheOraclesBoostForATargetHasSettled()
    {
        var (scenario, trace) = Night(new ScenarioEvent(1, NoiseClass.Cat, 1_000, 1_100, 20));
        Boost(trace.OracleBoost, 1_000, 1_500, 20);
        Boost(trace.Boost, 1_000, 1_500, 20);
        Boost(trace.Boost, 1_600, 1_700, 10);

        var result = PassResult.Of(scenario, trace, new MaskerSettings());

        Assert.Equal(100.0 / (Hops - Settle - 500), result.NightQuietBoostedShare, 6);
        Assert.Equal(0, result.NightQuietBoostedShareVersion2);
    }

    [Fact]
    public void AnOraclesReleaseLongerThanThirteenSecondsIsLeftOut()
    {
        var (scenario, trace) = Night(new ScenarioEvent(1, NoiseClass.Car, 1_000, 1_100, 20));
        Boost(trace.OracleBoost, 1_000, 2_700, 20);
        Boost(trace.Boost, 1_000, 2_700, 20);

        var result = PassResult.Of(scenario, trace, new MaskerSettings());

        Assert.Equal(0, result.NightQuietBoostedShare);
        Assert.True(result.NightQuietBoostedShareVersion2 > 0);
    }

    [Fact]
    public void AFalseAlarmsTailAfterASoundThatMustStayAudibleCounts()
    {
        var (scenario, trace) = Night(new ScenarioEvent(1, NoiseClass.Other, 1_000, 1_100, 20));
        Boost(trace.Boost, 1_000, 1_400, 20);

        var result = PassResult.Of(scenario, trace, new MaskerSettings());

        Assert.Equal(300.0 / (Hops - Settle - 100), result.NightQuietBoostedShare, 6);
        Assert.Contains(result.Failures, f => f.StartsWith("quiet night", StringComparison.Ordinal));
    }

    [Fact]
    public void AMissedBriefTargetFailsTheWorstSoundCheck()
    {
        var (scenario, trace) = Night(
            new ScenarioEvent(1, NoiseClass.Motorcycle, 1_000, 1_300, 20),
            new ScenarioEvent(2, NoiseClass.Motorcycle, 2_000, 2_020, 20));
        Boost(trace.OracleBoost, 1_000, 1_300, 30);
        Boost(trace.Boost, 1_000, 1_300, 30);
        Boost(trace.OracleBoost, 2_000, 2_020, 30);

        var result = PassResult.Of(scenario, trace, new MaskerSettings());

        Assert.Equal(1, result.WorstEventOracleGap, 6);
        Assert.Equal(0, result.WorstRecognisableOracleGap, 6);
        Assert.Contains(result.Failures, f => f.StartsWith("worst sound", StringComparison.Ordinal));
    }

    /// <summary>A steady room at -60 dB in every band, with each event's sound at -30 dB while it lasts.</summary>
    private static (NightScenario Scenario, NightTrace Trace) Night(params ScenarioEvent[] events)
    {
        var samples = Hops * ListeningFormat.HopSamples;
        var scenario = new NightScenario("motorcycle", 0, new float[samples], new float[samples], events);
        var trace = NightTrace.Empty(Hops);
        Array.Fill(trace.RoomPower, RoomPower);
        Array.Fill(trace.RoomPeakDb, HopAnalyzer.ToDb(RoomPower));
        trace.HopMilliseconds.Add(0.1);
        foreach (var e in events)
            for (var h = e.StartHop; h < e.EndHop; h++)
                trace.SoundPower.AsSpan(h * MaskerBands.Count, MaskerBands.Count).Fill(SoundPower);
        return (scenario, trace);
    }

    private static void Boost(float[] boosts, int from, int to, float db)
    {
        for (var h = from; h < to; h++)
            boosts.AsSpan(h * MaskerBands.Count, MaskerBands.Count).Fill(db);
    }
}
