using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;
using QuietNight.Core.Masking;

namespace QuietNight.Night;

/// <summary>How much a noise must rise before the masker reacts.</summary>
public enum Sensitivity
{
    Gentle,
    Normal,
    Sensitive,
}

/// <summary>The user's bedtime choices, kept in app preferences.</summary>
public sealed record NightSettings(float Volume, float MaxBoostDb, Sensitivity Sensitivity)
{
    public const float LoopLevelDb = -15;
    private const float VolumeRangeDb = 30;

    public static NightSettings Default { get; } = new(0.5f, ListenerSettings.Default.Masker.MaxBoostDb, Sensitivity.Normal);

    public float HeadroomDb => Sensitivity switch
    {
        Sensitivity.Gentle => 10,
        Sensitivity.Sensitive => 3,
        _ => 6,
    };

    public ListenerSettings Listener => ListenerSettings.Default with
    {
        Masker = ListenerSettings.Default.Masker with { HeadroomDb = HeadroomDb, MaxBoostDb = MaxBoostDb },
    };

    /// <summary>
    /// The bedtime level of each layer's track, leaving room for the largest boost below full volume. The phone's media
    /// volume scales everything on top.
    /// </summary>
    public void BedtimeLevels(Span<float> levelDb)
    {
        var top = -(MaxBoostDb + MaskerSettings.ShapeDb.Max());
        var volumeDb = top - (1 - Math.Clamp(Volume, 0, 1)) * VolumeRangeDb;
        for (var b = 0; b < MaskerBands.Count; b++)
            levelDb[b] = volumeDb + MaskerSettings.ShapeDb[b];
    }

    public static NightSettings Load() => new(
        Preferences.Default.Get("volume", Default.Volume),
        Preferences.Default.Get("max-boost", Default.MaxBoostDb),
        (Sensitivity)Preferences.Default.Get("sensitivity", (int)Default.Sensitivity));

    public void Save()
    {
        Preferences.Default.Set("volume", Volume);
        Preferences.Default.Set("max-boost", MaxBoostDb);
        Preferences.Default.Set("sensitivity", (int)Sensitivity);
    }
}
