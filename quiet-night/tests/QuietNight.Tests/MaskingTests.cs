using QuietNight.Core.Analysis;
using QuietNight.Core.Masking;

namespace QuietNight.Tests;

public sealed class MaskingTests
{
    private static readonly MaskerSettings Settings = new(HeadroomDb: 6, MaxBoostDb: 30, MarginDb: 3, HoldSeconds: 3, ReleaseDbPerSecond: 3);

    [Theory]
    [InlineData(-3f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(1f, 4f)]
    [InlineData(14f, 17f)]
    [InlineData(40f, 30f)]
    public void NeededBoostCoversTheExcessWithAMarginUpToTheLimit(float excess, float expected) =>
        Assert.Equal(expected, Settings.Needed(excess), 1e-4f);

    [Fact]
    public void BedtimeMaskerIsAnchoredAtTheRoomsLoudestBandForItsShape()
    {
        float[] room = [-50, -60, -70, -80, -80, -80, -80];
        var covered = new float[MaskerBands.Count];
        Settings.Covered(room, covered);
        Assert.Equal([-44f, -45f, -46f, -48f, -50f, -53f, -57f], covered);
        Assert.All(Enumerable.Range(0, room.Length), b => Assert.True(covered[b] >= room[b] + Settings.HeadroomDb));
    }

    [Fact]
    public void BoostRisesAtOnceHoldsThenFadesAtTheReleaseRate()
    {
        var masker = new MaskerController(Settings);
        var loud = Excess(14);
        var quiet = Excess(-10);
        masker.Update(true, loud, Room, confirmed: true);
        Assert.All(masker.BoostDb.ToArray(), b => Assert.Equal(17, b, 1e-4f));
        var holdHops = ListeningFormat.Hops(TimeSpan.FromSeconds(Settings.HoldSeconds));
        for (var h = 0; h < holdHops - 1; h++)
            masker.Update(true, quiet, Room);
        Assert.Equal(17, masker.LoudestBoostDb, 1e-4f);
        for (var h = 0; h < 100; h++)
            masker.Update(true, quiet, Room);
        Assert.Equal(17 - Settings.ReleaseDbPerSecond, masker.LoudestBoostDb, 0.05f);
        for (var h = 0; h < 1_000; h++)
            masker.Update(true, quiet, Room);
        Assert.Equal(0, masker.LoudestBoostDb);
    }

    [Fact]
    public void UnconfirmedBoostsAreHeldBrieflyAndFadeQuickly()
    {
        var masker = new MaskerController(Settings);
        masker.Update(true, Excess(14), Room);
        var holdHops = ListeningFormat.Hops(TimeSpan.FromSeconds(Settings.ProvisionalHoldSeconds));
        for (var h = 0; h < holdHops + 100; h++)
            masker.Update(true, Excess(-10), Room);
        Assert.Equal(17 - Settings.ProvisionalReleaseDbPerSecond, masker.LoudestBoostDb, 0.15f);
    }

    [Fact]
    public void AConfirmedRunKeepsEachBandNearItsPeak()
    {
        var settings = Settings with { RunDipDb = 6 };
        var masker = new MaskerController(settings);
        masker.Update(true, Excess(20), Room, confirmed: true);
        for (var h = 0; h < 2_000; h++)
            masker.Update(true, Excess(-10), Room, confirmed: true);
        Assert.Equal(23 - settings.RunDipDb, masker.LoudestBoostDb, 1e-4f);
        for (var h = 0; h < 2_000; h++)
            masker.Update(true, Excess(-10), Room, confirmed: false);
        Assert.Equal(0, masker.LoudestBoostDb);
    }

    [Fact]
    public void ClosedGateNeverRaisesTheMasker()
    {
        var masker = new MaskerController(Settings);
        for (var h = 0; h < 50; h++)
            masker.Update(false, Excess(25), Room);
        Assert.Equal(0, masker.LoudestBoostDb);
    }

    [Fact]
    public void EachBandOnlyRisesAsFarAsItsOwnSoundNeeds()
    {
        var masker = new MaskerController(Settings);
        float[] excess = [30, 15, -2, 0, -6, 8, 0];
        var level = excess.Select((e, b) => Room[b] + Settings.HeadroomDb + e).ToArray();
        masker.Update(true, level, Room);
        Assert.Equal(excess.Select(Settings.Needed).ToArray(), masker.BoostDb.ToArray());
    }

    [Theory]
    [InlineData(0, 31.25, 125.0)]
    [InlineData(3, 500.0, 1_000.0)]
    [InlineData(6, 4_000.0, 12_000.0)]
    public void NoiseLoopsStayInTheirBandAtTheRequestedLevel(int band, double low, double high)
    {
        Assert.Equal((low, high), MaskerBands.LayerHertz[band]);
        const int log2 = 15;
        var loop = NoiseLoop.Create(low, high, seed: band, rmsDb: -20, lengthLog2: log2);
        Assert.Equal(-20, Loudness.RmsDb(loop), 0.01f);
        var real = loop.ToArray();
        var imaginary = new float[real.Length];
        new Fft(real.Length).Forward(real, imaginary);
        var hertzPerBin = (double)NoiseLoop.OutputSampleRate / real.Length;
        double inside = 0, total = 0;
        for (var k = 1; k < real.Length / 2; k++)
        {
            var power = real[k] * real[k] + imaginary[k] * imaginary[k];
            total += power;
            if (k * hertzPerBin >= low - hertzPerBin && k * hertzPerBin <= high + hertzPerBin) inside += power;
        }
        Assert.True(inside / total > 0.999, $"only {inside / total:P2} inside the band");
    }

    [Fact]
    public void NoiseLoopsJoinWithoutAClick()
    {
        var loop = NoiseLoop.Create(1_000, 2_000, seed: 1, lengthLog2: 15);
        var steps = loop.Zip(loop.Skip(1), (a, b) => MathF.Abs(b - a)).ToArray();
        var seam = MathF.Abs(loop[0] - loop[^1]);
        Assert.True(seam <= steps.Max(), $"seam step {seam} exceeds every step inside the loop");
    }

    [Fact]
    public void LayersExistForEveryMaskingBand() =>
        Assert.Equal(MaskerBands.Count, NoiseLoop.CreateLayers(lengthLog2: 12).Length);

    /// <summary>A room shaped exactly like the masker, so every band's bedtime cover is the room plus the headroom.</summary>
    private static readonly float[] Room = [.. MaskerSettings.ShapeDb.Select(s => s - 60)];

    private static float[] Excess(float excess) => [.. Room.Select(r => r + Settings.HeadroomDb + excess)];
}
