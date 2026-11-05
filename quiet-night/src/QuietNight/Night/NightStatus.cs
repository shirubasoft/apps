using QuietNight.Core.Detection;

namespace QuietNight.Night;

/// <summary>A noise the app masked during the night.</summary>
public sealed record MaskedNoise(NoiseClass Noise, DateTimeOffset At, float LoudestBoostDb);

/// <summary>What the night session is doing right now, published a few times a second for the screen.</summary>
public sealed record NightStatus(
    bool Running,
    DateTimeOffset? Since,
    NoiseClass Masking,
    float LoudestBoostDb,
    float RoomDb,
    IReadOnlyList<MaskedNoise> Masked,
    string? Problem)
{
    public static NightStatus Idle { get; } = new(false, null, NoiseClass.Other, 0, float.NaN, [], null);
}
