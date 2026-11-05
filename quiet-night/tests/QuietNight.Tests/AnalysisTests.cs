using QuietNight.Core.Analysis;
using QuietNight.Tests.Support;

namespace QuietNight.Tests;

public sealed class AnalysisTests
{
    [Theory]
    [InlineData(8, 1)]
    [InlineData(64, 5)]
    [InlineData(512, 37)]
    [InlineData(4096, 1000)]
    public void FftFindsACosineAtItsBinAndInvertsExactly(int size, int bin)
    {
        var real = Enumerable.Range(0, size).Select(i => MathF.Cos(2 * MathF.PI * bin * i / size)).ToArray();
        var original = real.ToArray();
        var imaginary = new float[size];
        var fft = new Fft(size);
        fft.Forward(real, imaginary);
        var magnitude = real.Zip(imaginary, (r, i) => MathF.Sqrt(r * r + i * i)).ToArray();
        Assert.Equal(size / 2f, magnitude[bin], size * 1e-4f);
        Assert.Equal(size / 2f, magnitude[size - bin], size * 1e-4f);
        Assert.True(magnitude.Where((_, k) => k != bin && k != size - bin).Max() < size * 1e-3f);
        fft.Inverse(real, imaginary);
        for (var i = 0; i < size; i++)
            Assert.Equal(original[i], real[i], 1e-4f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(1000)]
    public void FftRejectsSizesThatAreNotPowersOfTwo(int size) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fft(size));

    [Theory]
    [InlineData(70, 0.5f, 0)]
    [InlineData(180, 0.1f, 1)]
    [InlineData(400, 0.25f, 2)]
    [InlineData(700, 0.05f, 3)]
    [InlineData(1500, 0.3f, 4)]
    [InlineData(3000, 0.01f, 5)]
    [InlineData(6000, 0.2f, 6)]
    public void SineLevelsLandInTheirOctaveBand(double hertz, float amplitude, int band)
    {
        var analyzer = new HopAnalyzer();
        foreach (var hop in Signals.Hops(Signals.Sine(hertz, amplitude, TimeSpan.FromSeconds(0.5))))
            analyzer.Process(hop.Span);
        var expected = 20 * MathF.Log10(amplitude) - 3;
        Assert.Equal(expected, analyzer.BandDb[band], 1.5f);
        for (var other = 0; other < MaskerBands.Count; other++)
            if (Math.Abs(other - band) > 1)
                Assert.True(analyzer.BandDb[other] < expected - 25, $"band {other} leaked {analyzer.BandDb[other]:F1} dB");
    }

    [Fact]
    public void AnalyzerRejectsHopsOfTheWrongLength() =>
        Assert.Throws<ArgumentException>(() => new HopAnalyzer().Process(new float[100]));

    /// <param name="before">The room's level for the first 30 s.</param>
    /// <param name="after">The level for the following stretch.</param>
    /// <param name="seconds">How long the new level lasts.</param>
    /// <param name="frozen">Whether the caller is masking that sound.</param>
    /// <param name="expected">The room's median level afterwards.</param>
    [Theory]
    [InlineData(-40f, -20f, 5.0, false, -40f)]
    [InlineData(-40f, -20f, 20.0, false, -20f)]
    [InlineData(-40f, -50f, 20.0, false, -50f)]
    [InlineData(-40f, -20f, 20.0, true, -40f)]
    public void RoomMedianIgnoresPassingSoundsAndAdoptsLastingOnes(float before, float after, double seconds, bool frozen, float expected)
    {
        var tracker = new BackgroundTracker(1);
        for (var h = 0; h < 3_000; h++)
            tracker.Update([before], false);
        Assert.Equal(before, tracker.Level[0], 0.5f);
        for (var h = 0; h < ListeningFormat.Hops(TimeSpan.FromSeconds(seconds)); h++)
            tracker.Update([after], frozen);
        Assert.Equal(expected, tracker.Level[0], 0.5f);
        Assert.True(tracker.Warm);
    }

    [Fact]
    public void RoomPeakIsTheUsualLoudestLevelAndStartupSilenceDoesNotDragTheRoomDown()
    {
        var tracker = new BackgroundTracker(1);
        for (var h = 0; h < 3; h++)
            tracker.Update([-120f], false);
        for (var h = 3; h < 3_000; h++)
            tracker.Update([h % 10 == 0 ? -30f : -40f], false);
        Assert.Equal(-40, tracker.Level[0], 0.5f);
        Assert.Equal(-30, tracker.Peak[0], 0.5f);
    }

    [Theory]
    [InlineData(-30f, -50f)]
    [InlineData(-60f, -45f)]
    public void LoudnessScalesClipsToTheirActiveLevel(float startDb, float targetDb)
    {
        var tone = Signals.Sine(500, MathF.Pow(10, startDb / 20) * MathF.Sqrt(2), TimeSpan.FromSeconds(1));
        var clip = Signals.Concat(tone, new float[Signals.Samples(TimeSpan.FromSeconds(1))]);
        Assert.Equal(startDb, Loudness.ActiveRmsDb(clip), 0.2f);
        Loudness.ScaleTo(clip, targetDb);
        Assert.Equal(targetDb, Loudness.ActiveRmsDb(clip), 0.2f);
        Assert.Equal(targetDb - 3, Loudness.RmsDb(clip), 0.3f);
    }

    [Fact]
    public void MelBandsCoverTheSpectrumWithoutGaps()
    {
        var bank = new MelBank();
        var power = Enumerable.Repeat(1f, ListeningFormat.Bins).ToArray();
        var bands = new float[bank.Bands];
        bank.Apply(power, bands);
        Assert.All(bands, b => Assert.True(b > 0));
    }
}
