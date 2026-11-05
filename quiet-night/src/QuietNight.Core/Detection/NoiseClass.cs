namespace QuietNight.Core.Detection;

/// <summary>What a sound is, as far as masking is concerned. Everything that should not be masked is <see cref="Other"/>.</summary>
public enum NoiseClass : byte
{
    Other = 0,
    Cat = 1,
    Motorcycle = 2,
    Car = 3,
}

public static class NoiseClasses
{
    public const int Count = 4;

    public static readonly NoiseClass[] Targets = [NoiseClass.Cat, NoiseClass.Motorcycle, NoiseClass.Car];

    public static bool IsTarget(this NoiseClass noise) => noise != NoiseClass.Other;

    /// <summary>The most likely target class, ignoring <see cref="NoiseClass.Other"/>.</summary>
    public static NoiseClass BestTarget(ReadOnlySpan<float> probabilities)
    {
        var best = NoiseClass.Cat;
        foreach (var target in Targets)
            if (probabilities[(int)target] > probabilities[(int)best])
                best = target;
        return best;
    }

    /// <summary>The probability that the sound is any target.</summary>
    public static float TargetProbability(ReadOnlySpan<float> probabilities) => 1 - probabilities[(int)NoiseClass.Other];
}
