using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Night;
using QuietNight.Core.Training;
using QuietNight.Tests.Support;

namespace QuietNight.Tests;

public sealed class DetectionTests
{
    private static readonly ListenerSettings Settings = ListenerSettings.Default;

    /// <summary>Half-weight windows and wider evidence thresholds, so each mechanism shows on its own.</summary>
    private static readonly ListenerSettings Gradual = ListenerSettings.Default with
    {
        WindowWeight = 0.5f, ConfirmEvidence = 2.5f, RejectEvidence = -2.5f,
    };

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
    public void RejectedSoundsStayUnmaskedAndOnlyTheirRepeatsStartRejected()
    {
        var gate = new NoiseGate(Gradual);
        Hops(gate, 3, needed: true, Low);
        Assert.True(gate.Open);
        Assert.False(gate.Apply(new Rejected(0.03f, default), gate.Episode));
        Assert.True(gate.Apply(new Rejected(0.03f, default), gate.Episode));
        Hops(gate, 200, needed: true, Low);
        Assert.False(gate.Open);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1)), needed: false);

        // The same sound again is rejected as soon as it is recognised, unless the sound model says otherwise.
        Hops(gate, NoiseGate.CompareHops + 2, needed: true, Low);
        Assert.False(gate.Open);
        Assert.True(gate.Rejecting);
        gate.Apply(new Unsure(NoiseClass.Cat, 0.9f, default), gate.Episode);
        Hops(gate, 2, needed: true, Low);
        Assert.True(gate.Open);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1)), needed: false);

        // A different sound is masked once it is clear it differs.
        Hops(gate, NoiseGate.CompareHops - 1, needed: true, High);
        Assert.False(gate.Open);
        Hops(gate, 2, needed: true, High);
        Assert.True(gate.Open);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1 + Gradual.RejectedRunSeconds)), needed: false);

        // Long after the rejected sound, even its repeat starts afresh.
        Hops(gate, 2, needed: true, Low);
        Assert.True(gate.Open);
    }

    [Fact]
    public void AClearNewTargetIsNamedCorrectlyAfterALongRunOfAnother()
    {
        var gate = new NoiseGate(Settings);
        Hops(gate, 3, needed: true);
        for (var window = 0; window < 20; window++)
            gate.Apply(new Confirmed(NoiseClass.Car, 0.95f, new ClassProbabilities(0.05f, 0.02f, 0.03f, 0.9f)), gate.Episode);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1)), needed: false);
        Hops(gate, 3, needed: true);
        gate.Apply(new Confirmed(NoiseClass.Cat, 0.98f, new ClassProbabilities(0.02f, 0.96f, 0.01f, 0.01f)), gate.Episode);
        gate.Hop(0.9f, NoiseClass.Car, needed: true);
        Assert.Equal(NoiseClass.Cat, gate.Noise);
    }

    [Fact]
    public void VerdictsAboutAnEarlierSoundAreIgnored()
    {
        var gate = new NoiseGate(Settings);
        Hops(gate, 3, needed: true);
        var earlier = gate.Episode;
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1)), needed: false);
        Hops(gate, 3, needed: true);
        Assert.False(gate.Apply(new Rejected(0.01f, default), earlier));
        Assert.Equal(0, gate.Evidence);
    }

    [Fact]
    public void EvidenceAgainstATargetCountsOnlyAsFarAsTheSoundFilledTheWindow()
    {
        var gate = new NoiseGate(Settings);
        Hops(gate, 3, needed: true);
        gate.Apply(new Rejected(0.01f, default), gate.Episode, filled: 0.25f);
        Assert.Equal(-NoiseGate.MaxWindowEvidence * 0.25f / Settings.FullWeightFill, gate.Evidence, 1e-4f);
        Assert.False(gate.Rejecting);
        Assert.True(gate.Apply(new Rejected(0.01f, default), gate.Episode, filled: Settings.FullWeightFill));

        // Evidence for a target counts in full however little of the window the sound filled.
        var other = new NoiseGate(Settings);
        Hops(other, 3, needed: true);
        other.Apply(new Confirmed(NoiseClass.Cat, 0.99f, new ClassProbabilities(0.01f, 0.97f, 0.01f, 0.01f)), other.Episode, filled: 0.1f);
        Assert.True(other.Confirming);
    }

    [Fact]
    public void EvidenceAddsUpAcrossWindowsBeforeConfirming()
    {
        var gate = new NoiseGate(Gradual);
        Hops(gate, 3, needed: true);
        gate.Apply(new Confirmed(NoiseClass.Car, 0.95f, new ClassProbabilities(0.05f, 0.05f, 0.05f, 0.85f)), gate.Episode);
        Assert.False(gate.Confirming);
        gate.Apply(new Confirmed(NoiseClass.Car, 0.95f, new ClassProbabilities(0.05f, 0.05f, 0.05f, 0.85f)), gate.Episode);
        Assert.True(gate.Confirming);
        gate.Hop(0, NoiseClass.Cat, needed: true);
        Assert.Equal(NoiseClass.Car, gate.Noise);
    }

    [Fact]
    public void ARejectedSoundThatGrowsMuchLouderIsJudgedAfresh()
    {
        var gate = new NoiseGate(Settings);
        Loud(gate, 3, Low, 10);
        gate.Apply(new Rejected(0.01f, default), gate.Episode);
        Assert.True(gate.Rejecting);
        var rejected = gate.Episode;
        Loud(gate, 50, Low, 10 + Settings.NewSoundDb - 1);
        Assert.Equal(rejected, gate.Episode);
        Assert.False(gate.Open);

        // A different sound on top of it starts a new episode, which is masked once it is clear it differs.
        Loud(gate, NoiseGate.CompareHops + 1, High, 10 + Settings.NewSoundDb);
        Assert.NotEqual(rejected, gate.Episode);
        Assert.True(gate.Open);
    }

    [Fact]
    public void AConfirmedRunMasksSoundsLikeItWithoutWaiting()
    {
        var gate = new NoiseGate(Gradual);
        Hops(gate, 3, needed: true, Low);
        for (var window = 0; window < 2; window++)
            gate.Apply(new Confirmed(NoiseClass.Motorcycle, 0.999f, new ClassProbabilities(0.001f, 0.02f, 0.929f, 0.05f)), gate.Episode);
        Assert.True(gate.Confirming);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1)), needed: false);
        Assert.True(gate.Confirming);

        // A different sound in the run waits for its own evidence; the fast detector here says it is no target.
        Silent(gate, NoiseGate.CompareHops + 2, High);
        Assert.False(gate.Open);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(1.1)), needed: false);

        // The same engine again is masked as part of the run.
        Silent(gate, NoiseGate.CompareHops, Low);
        Assert.True(gate.Open);
        Assert.Equal(NoiseClass.Motorcycle, gate.Noise);
        Hops(gate, ListeningFormat.Hops(TimeSpan.FromSeconds(Settings.ConfirmedSeconds + 2)), needed: false);
        Silent(gate, NoiseGate.CompareHops, Low);
        Assert.False(gate.Open);
    }

    /// <summary>A sound rising in the low mel bands, like an engine.</summary>
    private static readonly float[] Low = [.. Enumerable.Range(0, ListeningFormat.MelBands).Select(m => m < 8 ? 1f - m / 8f : 0f)];

    /// <summary>A sound rising in the high mel bands, like a door's squeak.</summary>
    private static readonly float[] High = [.. Enumerable.Range(0, ListeningFormat.MelBands).Select(m => m >= 24 ? (m - 23) / 8f : 0f)];

    private static void Loud(NoiseGate gate, int count, float[] rise, float loudestDb)
    {
        for (var h = 0; h < count; h++)
            gate.Hop(0.9f, NoiseClass.Car, needed: true, rise, loudestDb);
    }

    private static void Silent(NoiseGate gate, int count, float[] rise)
    {
        for (var h = 0; h < count; h++)
            gate.Hop(0, NoiseClass.Cat, needed: true, rise);
    }

    private static void Hops(NoiseGate gate, int count, bool needed, float[]? rise = null)
    {
        for (var h = 0; h < count; h++)
            gate.Hop(0.9f, NoiseClass.Car, needed, rise);
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
