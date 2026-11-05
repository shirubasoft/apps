namespace QuietNight.Core.Analysis;

/// <summary>
/// Octave bands shared by the microphone analysis and the masking noise layers.
/// The top layer extends above the microphone's 8 kHz limit and follows the 4-8 kHz band.
/// </summary>
public static class MaskerBands
{
    public const int Count = 7;

    private static readonly int[] FirstBin = [1, 4, 8, 16, 32, 64, 128];
    private static readonly int[] EndBin = [4, 8, 16, 32, 64, 128, ListeningFormat.Bins];

    /// <summary>Lower and upper edges, in hertz, of each masking noise layer.</summary>
    public static readonly (double Low, double High)[] LayerHertz =
    [
        (31.25, 125), (125, 250), (250, 500), (500, 1_000), (1_000, 2_000), (2_000, 4_000), (4_000, 12_000),
    ];

    public static int First(int band) => FirstBin[band];

    public static int End(int band) => EndBin[band];
}
