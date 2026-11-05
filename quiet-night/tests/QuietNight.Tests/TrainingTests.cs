using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Recordings;
using QuietNight.Core.Training;
using QuietNight.Tests.Support;

namespace QuietNight.Tests;

public sealed class TrainingTests
{
    [Fact]
    public void SoftmaxLearnsSeparableClassesAndSurvivesARoundTrip()
    {
        var data = Clusters(perClass: 200, seed: 1);
        var model = SoftmaxTrainer.Fit(data, new TrainingOptions(Epochs: 15, LearningRate: 0.02f, L2: 0.01f));
        Assert.Equal(1, SoftmaxTrainer.Accuracy(model, data), 3);
        using var stream = new MemoryStream();
        model.Write(stream);
        stream.Position = 0;
        var copy = SoftmaxModel.Read(stream);
        Span<float> a = stackalloc float[NoiseClasses.Count];
        Span<float> b = stackalloc float[NoiseClasses.Count];
        for (var i = 0; i < data.Count; i += 37)
        {
            model.Predict(data.Row(i), a);
            copy.Predict(data.Row(i), b);
            Assert.Equal(a.ToArray(), b.ToArray());
        }
    }

    [Fact]
    public void FineTuningLearnsANewClusterWithoutForgettingTheOldOnes()
    {
        var old = Clusters(perClass: 200, seed: 2);
        var model = SoftmaxTrainer.Fit(old, new TrainingOptions(Epochs: 15, LearningRate: 0.02f, L2: 0.01f));
        var shifted = new LabeledSet(old.Features);
        var random = new Random(3);
        for (var i = 0; i < 100; i++)
            shifted.Add([3 + Jitter(random), 3 + Jitter(random), Jitter(random)], NoiseClass.Cat);
        Assert.True(SoftmaxTrainer.Accuracy(model, shifted) < 0.5);
        var data = new LabeledSet(old.Features);
        data.AddRange(old);
        data.AddRange(shifted, 3);
        var tuned = SoftmaxTrainer.FineTune(model, data, new TrainingOptions(Epochs: 30, LearningRate: 0.02f, L2: 0, AnchorL2: 0.01f));
        Assert.True(SoftmaxTrainer.Accuracy(tuned, shifted) > 0.95);
        Assert.True(SoftmaxTrainer.BalancedAccuracy(tuned, old) > 0.95);
    }

    [Fact]
    public void LabeledSetsKeepTheirRowsAtHalfPrecisionAndSplitPerClass()
    {
        var data = Clusters(perClass: 10, seed: 4);
        using var stream = new MemoryStream();
        data.Write(stream);
        stream.Position = 0;
        var copy = LabeledSet.Read(stream);
        Assert.Equal(data.Count, copy.Count);
        for (var i = 0; i < data.Count; i++)
        {
            Assert.Equal(data.Label(i), copy.Label(i));
            for (var d = 0; d < data.Features; d++)
                Assert.Equal(data.Row(i)[d], copy.Row(i)[d], 0.01f);
        }
        var (training, validation) = data.Split(0.2, seed: 1);
        Assert.Equal(data.Count, training.Count + validation.Count);
        Assert.All(Enum.GetValues<NoiseClass>(), c => Assert.Equal(2, validation.CountOf(c)));
        Assert.All(Enum.GetValues<NoiseClass>(), c => Assert.Equal(3, data.Sample(3, seed: 1).CountOf(c)));
    }

    [Fact]
    public void RecordingsAreSavedListedLoadedRelabelledAndDeleted()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"quiet-night-{Guid.NewGuid():N}");
        try
        {
            var library = new RecordingLibrary(directory);
            Assert.Empty(library.List());
            var tone = Signals.Sine(440, 0.5f, TimeSpan.FromSeconds(1.5));
            var cat = library.Save(tone, NoiseClass.Cat, new DateTimeOffset(2026, 11, 5, 23, 15, 2, 123, TimeSpan.Zero));
            library.Save(tone, NoiseClass.Other, new DateTimeOffset(2026, 11, 5, 23, 16, 0, TimeSpan.Zero));
            File.WriteAllText(Path.Combine(directory, "notes.wav"), "not a recording");
            var listed = library.List();
            Assert.Equal([NoiseClass.Other, NoiseClass.Cat], listed.Select(r => r.Label));
            Assert.Equal(TimeSpan.FromSeconds(1.5), listed[1].Duration);
            Assert.Equal(cat.Created, listed[1].Created);
            var loaded = library.Load(listed[1]);
            Assert.Equal(tone.Length, loaded.Length);
            for (var i = 0; i < tone.Length; i += 101)
                Assert.Equal(tone[i], loaded[i], 1e-4f);
            var relabelled = library.Relabel(listed[1], NoiseClass.Motorcycle);
            Assert.Equal(NoiseClass.Motorcycle, Assert.Single(library.List(), r => r.Created == cat.Created).Label);
            Assert.Equal(tone.Length, library.Load(relabelled).Length);
            library.Delete(relabelled);
            Assert.Single(library.List());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PersonalTrainingKeepsNewHeadsOnlyWhenTheyPassItsOwnRule()
    {
        using var model = TestAssets.CreateSoundModel();
        var trainer = new PersonalTrainer(model, ListenerSettings.Default);
        var current = TestAssets.Heads();
        var reference = TestAssets.Reference();
        // A warbling tone stands in for the user's cat and a steady hiss for their fan; each is long enough for its
        // last fifth to be held back.
        var cat = Signals.Concat([.. Enumerable.Range(0, 8).Select(i => Signals.Concat(
            Signals.Sine(600 + 80 * (i % 3), 0.3f, TimeSpan.FromSeconds(0.6)), new float[Signals.Samples(TimeSpan.FromSeconds(0.4))]))]);
        var fan = Signals.Noise(0.02f, TimeSpan.FromSeconds(8), seed: 5);
        var progress = new List<double>();

        var (heads, report) = trainer.Train(
            [new LabeledAudio(cat, NoiseClass.Cat), new LabeledAudio(fan, NoiseClass.Other)],
            current, reference, new InlineProgress(progress.Add));

        Assert.Equal(2, report.Recordings);
        Assert.True(report.FrameExamples > 0 && report.SoundExamples > 0);
        Assert.False(double.IsNaN(report.SoundBefore) || double.IsNaN(report.SoundAfter));
        // The decision follows the documented rule: a clear gain on the held-back recordings, and no loss beyond the
        // allowances there or on reference sounds the heads never trained on.
        var gains = report.SoundAfter >= report.SoundBefore + PersonalTrainer.RequiredGain
            && report.FrameAfter >= report.FrameBefore - PersonalTrainer.AllowedPersonalLoss;
        if (report.Accepted)
        {
            Assert.True(gains);
            Assert.NotSame(current.Sound, heads.Sound);
        }
        else Assert.Same(current, heads);
        Assert.Equal(1, progress[^1], 1e-9);
        Assert.Equal(progress.Order(), progress);
    }

    [Fact]
    public void PersonalTrainingWithoutRecordingsKeepsTheCurrentHeads()
    {
        using var model = TestAssets.CreateSoundModel();
        var current = TestAssets.Heads();
        var (heads, report) = new PersonalTrainer(model, ListenerSettings.Default).Train([], current, TestAssets.Reference());
        Assert.False(report.Accepted);
        Assert.Same(current, heads);
        Assert.True(double.IsNaN(report.SoundBefore));
        Assert.False(double.IsNaN(report.ReplayBefore));
    }

    /// <summary>Reports progress synchronously, unlike <see cref="Progress{T}"/>, so a test sees every value in order.</summary>
    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    [Theory]
    [InlineData(NoiseClass.Cat, "cat")]
    [InlineData(NoiseClass.Motorcycle, "motorcycle")]
    [InlineData(NoiseClass.Car, "car")]
    [InlineData(NoiseClass.Other, "other")]
    public void RecordingNamesCarryTheirLabel(NoiseClass label, string slug) =>
        Assert.Equal(slug, RecordingLibrary.Slug(label));

    [Fact]
    public void WaveFilesMixStereoDownAndRejectOtherFormats()
    {
        using var stereo = new MemoryStream();
        using (var writer = new BinaryWriter(stereo, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8); writer.Write(36 + 8); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)2); writer.Write(16_000); writer.Write(64_000);
            writer.Write((short)4); writer.Write((short)16); writer.Write("data"u8); writer.Write(8);
            writer.Write((short)16384); writer.Write((short)0); writer.Write((short)-16384); writer.Write((short)-16384);
        }
        stereo.Position = 0;
        var (samples, rate) = WaveFile.Read(stereo);
        Assert.Equal(16_000, rate);
        Assert.Equal([0.25f, -0.5f], samples);
        Assert.Throws<InvalidDataException>(() => WaveFile.Read(new MemoryStream("RIFX0000"u8.ToArray())));
    }

    [Theory]
    [InlineData(NoiseClass.Cat)]
    [InlineData(NoiseClass.Other)]
    public void ExamplesLabelOnlyTheHopsWhereTheSoundStandsOut(NoiseClass label)
    {
        var builder = new ExampleBuilder(null, ListenerSettings.Default);
        var frames = new LabeledSet(FrameFeatures.Count);
        var bed = RoomBed.Create(lengthLog2: 16);
        var sound = Signals.Concat(new float[Signals.Samples(TimeSpan.FromSeconds(1))], Signals.Sine(600, 0.3f, TimeSpan.FromSeconds(1)), new float[Signals.Samples(TimeSpan.FromSeconds(1))]);
        builder.Add(bed, sound, label, 20, frames, null);
        // One second of tone at a stride of two hops, plus the 40 ms smoothing tail as the level decays.
        Assert.InRange(frames.Count, 45, 70);
        Assert.Equal(frames.Count, frames.CountOf(label));
    }

    private static LabeledSet Clusters(int perClass, int seed)
    {
        var random = new Random(seed);
        var data = new LabeledSet(3);
        float[][] centres = [[0, 0, 0], [4, 0, 0], [0, 4, 0], [0, 0, 4]];
        for (var i = 0; i < perClass; i++)
            foreach (var noise in Enum.GetValues<NoiseClass>())
            {
                var c = centres[(int)noise];
                data.Add([c[0] + Jitter(random), c[1] + Jitter(random), c[2] + Jitter(random)], noise);
            }
        return data;
    }

    private static float Jitter(Random random) => (float)(random.NextDouble() - 0.5);
}
