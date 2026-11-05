using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Training;
using QuietNight.Tests.Support;

namespace QuietNight.Tests.Evaluation;

public sealed record ScenarioEvent(int SoundId, NoiseClass Noise, int StartHop, int EndHop, float LevelDb);

/// <summary>
/// A simulated stretch of night: a held-out room recording at -50 dBFS, plus phone-microphone self-noise, with four
/// held-out target sounds of one category
/// and two held-out sounds that must stay audible or are harmless, so must not trigger masking, each followed by 15 s of
/// quiet. Every pass uses different clips, rooms, levels and order.
/// </summary>
public sealed record NightScenario(string Category, int Pass, float[] Room, float[] Sounds, IReadOnlyList<ScenarioEvent> Events)
{
    public const float RoomDb = -50;
    public const int Targets = 4;
    public const int Others = 2;
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan Gap = TimeSpan.FromSeconds(15);

    /// <summary>How far above the room each category is mixed: realistic indoor levels with closed windows.</summary>
    public static readonly IReadOnlyDictionary<string, (float Low, float High)> Levels = new Dictionary<string, (float, float)>
    {
        ["cat"] = (10, 25),
        ["motorcycle"] = (15, 30),
        ["car"] = (10, 25),
        ["other"] = (8, 22),
    };

    public static readonly string[] Categories = ["cat", "motorcycle", "car"];

    public int Hops => Room.Length / ListeningFormat.HopSamples;

    public float[] Mixture()
    {
        var mixture = new float[Room.Length];
        for (var i = 0; i < mixture.Length; i++)
            mixture[i] = Room[i] + Sounds[i];
        return mixture;
    }

    public static NightScenario Build(string category, int pass) => Build(category, pass, ReferenceClips.Test, Targets, Others);

    /// <summary>A scenario from any split; tuning uses the validation split with fewer sounds per pass.</summary>
    public static NightScenario Build(string category, int pass, ReferenceClips clips, int targetsPerPass, int othersPerPass)
    {
        var categoryIndex = Array.IndexOf(Categories, category);
        var slot = pass * Categories.Length + categoryIndex;
        var random = new Random(1_000 * pass + categoryIndex);
        var targets = clips.Group(category);
        var others = clips.Group("other");
        var rooms = clips.Group("background");
        if (targets.Count < targetsPerPass * (pass + 1) || others.Count < othersPerPass * (slot + 1) || rooms.Count == 0)
            throw new InvalidOperationException($"The {clips.Split} reference sounds cannot fill {category} pass {pass + 1}.");

        var picks = targets.Skip(targetsPerPass * pass).Take(targetsPerPass).Select(s => (Sound: s, Category: category))
            .Concat(others.Skip(othersPerPass * slot).Take(othersPerPass).Select(s => (Sound: s, Category: "other")))
            .OrderBy(_ => random.Next())
            .ToList();
        var settle = ListeningFormat.Hops(Settle) * ListeningFormat.HopSamples;
        var gap = ListeningFormat.Hops(Gap) * ListeningFormat.HopSamples;
        var length = settle + picks.Sum(p => clips.Audio(p.Sound).Length + gap) + ListeningFormat.SampleRate * 2;
        length -= length % ListeningFormat.HopSamples;

        var bed = RoomBed.Loop(clips.Audio(rooms[slot % rooms.Count]));
        Loudness.ScaleTo(bed, RoomDb);
        var room = new float[length];
        for (var i = 0; i < length; i++)
            room[i] = bed[i % bed.Length];
        RoomBed.AddMicrophoneNoise(room, seed: slot);

        var sounds = new float[length];
        var events = new List<ScenarioEvent>();
        var cursor = settle;
        foreach (var (sound, group) in picks)
        {
            var samples = clips.Audio(sound).ToArray();
            var (low, high) = Levels[group];
            var level = low + (float)random.NextDouble() * (high - low);
            Loudness.ScaleTo(samples, Loudness.RmsDb(bed) + level);
            samples.CopyTo(sounds.AsSpan(cursor));
            events.Add(new ScenarioEvent(sound.Id, sound.Noise, cursor / ListeningFormat.HopSamples,
                (cursor + samples.Length + ListeningFormat.HopSamples - 1) / ListeningFormat.HopSamples, level));
            cursor += samples.Length + gap;
        }
        return new NightScenario(category, pass, room, sounds, events);
    }
}
