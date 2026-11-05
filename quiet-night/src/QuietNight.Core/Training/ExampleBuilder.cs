using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Night;

namespace QuietNight.Core.Training;

/// <summary>
/// Turns a recorded sound into training examples for both detection stages. The sound is laid over a room recording
/// ("bed") after a few seconds of quiet, exactly as the listener would hear it at night, and each hop and window is
/// labelled from the sound alone: a hop counts as the sound's class only where the sound stands out from both its own
/// quiet parts and the room.
/// </summary>
public sealed class ExampleBuilder(SoundModel? soundModel, ListenerSettings settings)
{
    public static readonly TimeSpan Warmup = TimeSpan.FromSeconds(4);
    private const float OwnFloorMarginDb = 6;
    private const float ActiveWindowShare = 0.3f;
    private const float QuietWindowShare = 0.02f;

    private static readonly int WindowHops = SoundModel.WindowSamples / ListeningFormat.HopSamples;
    private static readonly int WindowStride = ListeningFormat.Hops(TimeSpan.FromSeconds(0.24));
    private static readonly SoftmaxModel OpenGate =
        SoftmaxModel.Zero(NoiseClasses.Count, new float[FrameFeatures.Count], [.. Enumerable.Repeat(1f, FrameFeatures.Count)]);

    private const int RoomWindows = 2;

    private readonly float[] raw = new float[SoundModel.FeatureCount];
    private readonly float[] roomRaw = new float[SoundModel.FeatureCount];
    private readonly float[] combined = new float[SoundRecognizer.FeatureCount];
    private readonly float[] window = new float[SoundModel.WindowSamples];

    public void Add(ReadOnlySpan<float> bed, ReadOnlySpan<float> sound, NoiseClass label, float levelAboveBedDb,
        LabeledSet frames, LabeledSet? windows, float weight = 1, int frameStride = 2)
    {
        if (bed.Length < ListeningFormat.HopSamples) throw new ArgumentException("The room recording is too short.", nameof(bed));
        var warmup = ListeningFormat.Hops(Warmup) * ListeningFormat.HopSamples;
        var tail = ListeningFormat.SampleRate / 2;
        var hops = (warmup + sound.Length + tail) / ListeningFormat.HopSamples;
        var placed = new float[hops * ListeningFormat.HopSamples];
        sound[..Math.Min(sound.Length, placed.Length - warmup)].CopyTo(placed.AsSpan(warmup));
        Loudness.ScaleTo(placed.AsSpan(warmup, Math.Min(sound.Length, placed.Length - warmup)), Loudness.RmsDb(bed) + levelAboveBedDb);
        var room = new float[placed.Length];
        for (var i = 0; i < room.Length; i++)
            room[i] = bed[i % bed.Length];
        RoomBed.AddMicrophoneNoise(room, seed: sound.Length);
        var mixture = new float[placed.Length];
        for (var i = 0; i < mixture.Length; i++)
            mixture[i] = room[i] + placed[i];

        var ownFloor = OwnFloorDb(placed.AsSpan(warmup, Math.Max(ListeningFormat.HopSamples, sound.Length))) + OwnFloorMarginDb;
        var listener = new NightListener(OpenGate, settings);
        var soundAlone = new HopAnalyzer();
        var roomAlone = new HopAnalyzer();
        var soundActive = new bool[WindowHops];
        var activeInWindow = 0;
        var listenerActiveInWindow = 0;
        var listenerHistory = new bool[WindowHops];
        var warmupHops = warmup / ListeningFormat.HopSamples;
        if (windows is not null && soundModel is not null)
            LearnRoom(room.AsSpan(0, warmup), soundModel, windows, weight);
        for (var h = 0; h < hops; h++)
        {
            var range = new Range(h * ListeningFormat.HopSamples, (h + 1) * ListeningFormat.HopSamples);
            listener.HoldRoom = label.IsTarget() && h >= warmupHops && listener.Active;
            listener.Process(mixture.AsSpan(range));
            soundAlone.Process(placed.AsSpan(range));
            roomAlone.Process(room.AsSpan(range));

            var dominates = false;
            var soundPower = 0f;
            for (var b = 0; b < MaskerBands.Count; b++)
            {
                soundPower += soundAlone.BandPower[b];
                dominates |= soundAlone.BandPower[b] > roomAlone.BandPower[b] && listener.RiseDb[b] >= settings.ActivityDb;
            }
            var eventActive = label.IsTarget() && dominates && HopAnalyzer.ToDb(soundPower) > ownFloor;
            var slot = h % WindowHops;
            activeInWindow += (eventActive ? 1 : 0) - (soundActive[slot] ? 1 : 0);
            soundActive[slot] = eventActive;
            listenerActiveInWindow += (listener.Active ? 1 : 0) - (listenerHistory[slot] ? 1 : 0);
            listenerHistory[slot] = listener.Active;
            if (h < warmupHops || !listener.Warm) continue;

            // The fast detector only decides for hops that stand out from the room.
            if (h % frameStride == 0 && listener.Active)
                frames.Add(listener.Features, eventActive ? label : NoiseClass.Other, weight);

            if (windows is null || soundModel is null || h < WindowHops || (h - warmupHops) % WindowStride != 0) continue;
            var share = (float)activeInWindow / WindowHops;
            NoiseClass? windowLabel = label.IsTarget()
                ? share >= ActiveWindowShare ? label : share <= QuietWindowShare && h % (WindowStride * 4) == 0 ? NoiseClass.Other : null
                : listenerActiveInWindow >= WindowHops / 10 || h % (WindowStride * 4) == 0 ? NoiseClass.Other : null;
            if (windowLabel is not { } known) continue;
            listener.CopyWindow(window);
            soundModel.Features(window, raw);
            SoundRecognizer.Combine(raw, roomRaw, combined);
            windows.Add(combined, known, weight);
        }
    }

    /// <summary>
    /// Learns the room from the quiet before the sound, as the app does, and adds a couple of room-only windows as
    /// examples of what must not be masked.
    /// </summary>
    private void LearnRoom(ReadOnlySpan<float> quiet, SoundModel model, LabeledSet windows, float weight)
    {
        Array.Clear(roomRaw);
        var last = quiet.Length - SoundModel.WindowSamples;
        quiet.Slice(last, SoundModel.WindowSamples).CopyTo(window);
        model.Features(window, roomRaw);
        for (var k = 1; k <= RoomWindows; k++)
        {
            quiet.Slice(last - k * SoundModel.WindowSamples / 2, SoundModel.WindowSamples).CopyTo(window);
            model.Features(window, raw);
            SoundRecognizer.Combine(raw, roomRaw, combined);
            windows.Add(combined, NoiseClass.Other, weight);
        }
    }

    /// <summary>The tenth-percentile hop level of the sound alone: its own quiet parts.</summary>
    private static float OwnFloorDb(ReadOnlySpan<float> sound)
    {
        var count = sound.Length / ListeningFormat.HopSamples;
        if (count == 0) return float.MinValue;
        var levels = new float[count];
        for (var h = 0; h < count; h++)
            levels[h] = Loudness.RmsDb(sound.Slice(h * ListeningFormat.HopSamples, ListeningFormat.HopSamples));
        Array.Sort(levels);
        return levels[count / 10];
    }
}
