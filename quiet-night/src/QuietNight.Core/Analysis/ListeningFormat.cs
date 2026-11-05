namespace QuietNight.Core.Analysis;

/// <summary>The microphone format every listening stage shares.</summary>
public static class ListeningFormat
{
    public const int SampleRate = 16_000;
    public const int HopSamples = 160;
    public const int WindowSamples = 400;
    public const int FftSize = 512;
    public const int Bins = FftSize / 2 + 1;
    public const int MelBands = 32;
    public const float HopSeconds = (float)HopSamples / SampleRate;
    public const float BinHertz = (float)SampleRate / FftSize;

    public static int Hops(TimeSpan duration) => (int)Math.Round(duration.TotalSeconds / HopSeconds);
}
