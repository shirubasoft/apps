using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Night;
using QuietNight.Core.Training;
using QuietNight.Tests.Support;

namespace QuietNight.Tests;

public sealed class DetectionTests
{
    private static readonly ListenerSettings Settings = ListenerSettings.Default;

    [Theory]
    [InlineData(new[] { 0.9f, 0.05f, 0.03f, 0.02f }, "Rejected", NoiseClass.Other)]
    [InlineData(new[] { 0.6f, 0.3f, 0.05f, 0.05f }, "Unsure", NoiseClass.Cat)]
    [InlineData(new[] { 0.1f, 0.1f, 0.7f, 0.1f }, "Confirmed", NoiseClass.Motorcycle)]
    [InlineData(new[] { 0.2f, 0.1f, 0.2f, 0.5f }, "Confirmed", NoiseClass.Car)]
    public void VerdictsFollowTheTargetProbability(float[] probabilities, string kind, NoiseClass noise)
    {
        var verdict = Settings.Judge(probabilities);
        var (actualKind, actualNoise) = verdict switch
        {
            Confirmed c => ("Confirmed", c.Noise),
            Unsure u => ("Unsure", u.Likely),
            Rejected => ("Rejected", NoiseClass.Other),
            _ => ("None", NoiseClass.Other),
        };
        Assert.Equal((kind, noise), (actualKind, actualNoise));
        Assert.Equal(1 - probabilities[0], verdict.TargetProbability, 1e-5f);
    }

    [Fact]
    public void SoundsThatNeedMaskingOpenTheGateAfterTwoHopsUnlessTheFastDetectorObjects()
    {
        var gate = new NoiseGate(Settings);
        gate.Hop(0.9f, NoiseClass.Cat, needed: true);
        Assert.False(gate.Open);
        gate.Hop(0.9f, NoiseClass.Cat, needed: true);
        Assert.True(gate.Open);
        Assert.Equal(NoiseClass.Cat, gate.Noise);
        gate.Hop(0.1f, NoiseClass.Cat, needed: true);
        Assert.False(gate.Open);
        gate.Hop(0.9f, NoiseClass.Cat, needed: false);
        Assert.False(gate.Open);
    }

    [Fact]
    public void RejectedSoundsStayUnmaskedAndTheirNeighboursStartRejected()
    {
        var gate = new NoiseGate(Settings);
        Hops(gate, 3, needed: true);
        Assert.True(gate.Open);
        Assert.True(gate.Apply(new Rejected(0.03f, default)));
        Hops(gate, 200, needed: true);
        Assert.False(gate.Open);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1)), needed: false);
        Hops(gate, 3, needed: true);
        Assert.False(gate.Open);
        gate.Apply(new Unsure(NoiseClass.Cat, 0.65f, default));
        Hops(gate, 2, needed: true);
        Assert.True(gate.Open);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1)), needed: false);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(NoiseGate.RejectedRunSeconds)), needed: false);
        Hops(gate, 2, needed: true);
        Assert.True(gate.Open);
    }

    [Fact]
    public void EvidenceAddsUpAcrossWindowsBeforeConfirming()
    {
        var gate = new NoiseGate(Settings);
        Hops(gate, 3, needed: true);
        gate.Apply(new Confirmed(NoiseClass.Car, 0.8f, new ClassProbabilities(0.2f, 0.05f, 0.05f, 0.7f)));
        Assert.False(gate.Confirming);
        gate.Apply(new Confirmed(NoiseClass.Car, 0.8f, new ClassProbabilities(0.2f, 0.05f, 0.05f, 0.7f)));
        Assert.True(gate.Confirming);
        gate.Hop(0, NoiseClass.Cat, needed: true);
        Assert.Equal(NoiseClass.Car, gate.Noise);
    }

    [Fact]
    public void AConfirmedRunMasksTheNextSoundsWithoutWaiting()
    {
        var gate = new NoiseGate(Settings);
        Hops(gate, 3, needed: true);
        gate.Apply(new Confirmed(NoiseClass.Motorcycle, 0.97f, new ClassProbabilities(0.03f, 0.02f, 0.9f, 0.05f)));
        Assert.True(gate.Confirming);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1)), needed: false);
        gate.Hop(0, NoiseClass.Cat, needed: true);
        Assert.True(gate.Open);
        Assert.Equal(NoiseClass.Motorcycle, gate.Noise);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(Settings.ConfirmedSeconds + 2)), needed: false);
        gate.Hop(0, NoiseClass.Cat, needed: true);
        Assert.False(gate.Open);
    }

    private static void Hops(NoiseGate gate, int count, bool needed)
    {
        for (var h = 0; h < count; h++)
            gate.Hop(0.9f, NoiseClass.Car, needed);
    }

    [Theory]
    [InlineData(true, 20f)]
    [InlineData(false, 0f)]
    public void ListenerMasksASuddenToneOnlyWhenTheDetectorCallsItATarget(bool target, float minimumBoost)
    {
        var listener = new NightListener(Always(target ? NoiseClass.Car : NoiseClass.Other), Settings);
        var room = Signals.Noise(0.003f, TimeSpan.FromSeconds(12), seed: 4);
        var tone = Signals.Sine(700, 0.3f, TimeSpan.FromSeconds(1));
        var mixture = Signals.Add(room, Signals.Concat(new float[Signals.Samples(TimeSpan.FromSeconds(10))], tone, new float[Signals.Samples(TimeSpan.FromSeconds(1))]));
        var toneHop = Signals.Samples(TimeSpan.FromSeconds(10)) / ListeningFormat.HopSamples;
        var hop = 0;
        int? firstBoost = null;
        foreach (var samples in Signals.Hops(mixture))
        {
            listener.Process(samples.Span);
            if (hop < toneHop) Assert.True(listener.Masker.LoudestBoostDb < 4, $"boosted {listener.Masker.LoudestBoostDb:F1} dB in a steady room");
            else if (listener.Masker.LoudestBoostDb > 4) firstBoost ??= hop;
            hop++;
        }
        if (target)
        {
            Assert.NotNull(firstBoost);
            Assert.InRange((firstBoost.Value - toneHop) * ListeningFormat.HopSeconds, 0, 0.03);
            Assert.True(listener.Masker.BoostDb[3] >= minimumBoost, $"band boost {listener.Masker.BoostDb[3]:F1} dB");
        }
        else
        {
            Assert.Null(firstBoost);
        }
    }

    [Fact]
    public void ListenerLearnsTheRoomFirstThenAsksForVerdictsOnlyWhileSomethingHappens()
    {
        var listener = new NightListener(Always(NoiseClass.Other), Settings);
        var quiet = Signals.Noise(0.003f, TimeSpan.FromSeconds(6), seed: 5);
        var burst = Signals.Add(Signals.Noise(0.003f, TimeSpan.FromSeconds(2), seed: 6), Signals.Sine(300, 0.2f, TimeSpan.FromSeconds(2)));
        var requests = new List<int>();
        var rooms = new List<int>();
        var hop = 0;
        var window = new float[SoundModel.WindowSamples];
        foreach (var samples in Signals.Hops(Signals.Concat(quiet, burst)))
        {
            listener.Process(samples.Span);
            if (listener.WantsRoom)
            {
                rooms.Add(hop);
                listener.RoomLearned();
            }
            if (listener.WantsVerdict)
            {
                requests.Add(hop);
                listener.CopyWindow(window);
                Assert.Equal(samples.Span[^1], window[^1]);
                listener.Accept(new Unsure(NoiseClass.Cat, 0.4f, default));
            }
            hop++;
        }
        var burstHop = quiet.Length / ListeningFormat.HopSamples;
        Assert.NotEmpty(rooms);
        Assert.True(rooms[0] < burstHop, "the room is learned before anything happens");
        Assert.NotEmpty(requests);
        Assert.All(requests, r => Assert.True(r >= burstHop));
        Assert.All(requests.Zip(requests.Skip(1)), pair => Assert.True(pair.Second - pair.First >= 24));
    }

    private static SoftmaxModel Always(NoiseClass noise)
    {
        var bias = new float[NoiseClasses.Count];
        bias[(int)noise] = 10;
        return new SoftmaxModel(NoiseClasses.Count, new float[FrameFeatures.Count], [.. Enumerable.Repeat(1f, FrameFeatures.Count)],
            new float[NoiseClasses.Count * FrameFeatures.Count], bias);
    }
}
