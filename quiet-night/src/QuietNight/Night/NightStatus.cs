using QuietNight.Core.Detection;

namespace QuietNight.Night;

/// <summary>A noise the app masked during the night.</summary>
public sealed record MaskedNoise(NoiseClass Noise, DateTimeOffset At, float LoudestBoostDb);

/// <summary>What the night session is doing right now, published a few times a second for the screen.</summary>
/// <param name="BandBoostDb">How far each masker band is raised above its bedtime level, from low to high pitch.</param>
/// <param name="Paused">Whether the masker is silent because another app holds the audio, such as during a call.</param>
/// <param name="MaxBoostDb">The loudest boost this night allows, which the bands and bars are drawn against.</param>
public sealed record NightStatus(
    bool Running,
    DateTimeOffset? Since,
    NoiseClass Masking,
    float LoudestBoostDb,
    float RoomDb,
    IReadOnlyList<MaskedNoise> Masked,
    string? Problem,
    IReadOnlyList<float> BandBoostDb,
    bool Paused = false,
    float MaxBoostDb = 30)
{
    public static NightStatus Idle { get; } = new(false, null, NoiseClass.Other, 0, float.NaN, [], null, []);
}

/// <summary>A finished night: when it ran, what it masked, and the loudest boost it allowed.</summary>
public sealed record NightSummary(DateTimeOffset Started, DateTimeOffset Ended, IReadOnlyList<MaskedNoise> Masked, float MaxBoostDb = 30)
{
    public int Count(NoiseClass noise) => Masked.Count(m => m.Noise == noise);

    /// <summary>How often the masking sound rose, such as "The masking sound rose 4 times", or null when it never did.</summary>
    public static string? Rises(IReadOnlyList<MaskedNoise> masked) => masked.Count switch
    {
        0 => null,
        1 => "The masking sound rose once",
        2 => "The masking sound rose twice",
        var n => $"The masking sound rose {n} times",
    };

    /// <summary>
    /// What the detector heard, such as "1 cat sound" or "3 cat sounds and 1 car sound", or null when nothing. These are
    /// sounds it heard, not animals or vehicles counted, and it can be wrong.
    /// </summary>
    public static string? Detected(IReadOnlyList<MaskedNoise> masked)
    {
        var parts = new List<string>();
        foreach (var (noise, one, many) in new[] { (NoiseClass.Cat, "cat sound", "cat sounds"), (NoiseClass.Motorcycle, "motorcycle sound", "motorcycle sounds"), (NoiseClass.Car, "car sound", "car sounds") })
        {
            var count = masked.Count(m => m.Noise == noise);
            if (count > 0) parts.Add(count == 1 ? $"1 {one}" : $"{count} {many}");
        }
        return parts.Count switch
        {
            0 => null,
            1 => parts[0],
            _ => $"{string.Join(", ", parts[..^1])} and {parts[^1]}",
        };
    }
}
