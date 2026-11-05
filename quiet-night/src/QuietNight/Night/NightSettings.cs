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
public sealed record NightSettings(float Volume, float MaxBoostDb, Sensitivity Sensitivity, IgnoredNoises Ignored)
{
    public const float LoopLevelDb = -15;
    private const float VolumeRangeDb = 30;

    public static NightSettings Default { get; } = new(0.5f, ListenerSettings.Default.Masker.MaxBoostDb, Sensitivity.Normal, default);

    public float HeadroomDb => Sensitivity switch
    {
        Sensitivity.Gentle => 10,
        Sensitivity.Sensitive => 3,
        _ => 6,
    };

    public ListenerSettings Listener => ListenerSettings.Default with
    {
        Ignored = Ignored,
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

    /// <summary>The noises this night masks, in words, such as "cats and cars".</summary>
    public string Masked()
    {
        var names = NoiseClasses.Targets.Where(n => !Ignored.Contains(n)).Select(n => Describe(n, plural: true)).ToList();
        return names.Count == 1 ? names[0] : $"{string.Join(", ", names[..^1])} and {names[^1]}";
    }

    /// <summary>A noise in words: "a cat", or "cats" when <paramref name="plural"/>.</summary>
    public static string Describe(NoiseClass noise, bool plural = false) => (noise, plural) switch
    {
        (NoiseClass.Cat, true) => "cats",
        (NoiseClass.Motorcycle, true) => "motorcycles",
        (NoiseClass.Car, true) => "cars",
        (NoiseClass.Cat, _) => "a cat",
        (NoiseClass.Motorcycle, _) => "a motorcycle",
        (NoiseClass.Car, _) => "a car",
        _ => plural ? "noises" : "a noise",
    };

    /// <summary>The time of day the night stops by itself, or null to run until stopped.</summary>
    public static TimeSpan? StopAt
    {
        get => Preferences.Default.Get("stop-at-minutes", -1) is var minutes and >= 0 ? TimeSpan.FromMinutes(minutes) : null;
        set => Preferences.Default.Set("stop-at-minutes", value is { } at ? (int)at.TotalMinutes : -1);
    }

    /// <summary>When a night started now would stop by itself, or null when it runs until stopped.</summary>
    public static DateTime? NextStop(DateTime now)
    {
        if (StopAt is not { } at) return null;
        var today = now.Date + at;
        return today > now.AddMinutes(1) ? today : today.AddDays(1);
    }

    public static NightSettings Load()
    {
        var ignored = new IgnoredNoises(
            Preferences.Default.Get("ignore-cats", false),
            Preferences.Default.Get("ignore-motorcycles", false),
            Preferences.Default.Get("ignore-cars", false));
        return new(
            Preferences.Default.Get("volume", Default.Volume),
            Preferences.Default.Get("max-boost", Default.MaxBoostDb),
            (Sensitivity)Preferences.Default.Get("sensitivity", (int)Default.Sensitivity),
            ignored.All ? default : ignored);
    }

    public void Save()
    {
        Preferences.Default.Set("volume", Volume);
        Preferences.Default.Set("max-boost", MaxBoostDb);
        Preferences.Default.Set("sensitivity", (int)Sensitivity);
        Preferences.Default.Set("ignore-cats", Ignored.Cats);
        Preferences.Default.Set("ignore-motorcycles", Ignored.Motorcycles);
        Preferences.Default.Set("ignore-cars", Ignored.Cars);
    }
}
