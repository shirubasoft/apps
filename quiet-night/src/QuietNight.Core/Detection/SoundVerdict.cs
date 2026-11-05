namespace QuietNight.Core.Detection;

/// <summary>The sound model is confident the window holds a target noise.</summary>
public record class Confirmed(NoiseClass Noise, float Probability, ClassProbabilities Classes);

/// <summary>The sound model is confident the window holds no target noise.</summary>
public record class Rejected(float Probability, ClassProbabilities Classes);

/// <summary>The sound model could not decide.</summary>
public record class Unsure(NoiseClass Likely, float Probability, ClassProbabilities Classes);

/// <summary>The sound model's probability for each <see cref="NoiseClass"/>.</summary>
public readonly record struct ClassProbabilities(float Other, float Cat, float Motorcycle, float Car)
{
    public static ClassProbabilities From(ReadOnlySpan<float> probabilities) =>
        new(probabilities[0], probabilities[1], probabilities[2], probabilities[3]);

    public float this[NoiseClass noise] => noise switch
    {
        NoiseClass.Cat => Cat,
        NoiseClass.Motorcycle => Motorcycle,
        NoiseClass.Car => Car,
        _ => Other,
    };
}

/// <summary>What the sound model concluded about one 0.96 s window.</summary>
public union SoundVerdict(Confirmed, Rejected, Unsure)
{
    public float TargetProbability => this switch
    {
        Confirmed c => c.Probability,
        Rejected r => r.Probability,
        Unsure u => u.Probability,
        _ => 0,
    };

    public ClassProbabilities Classes => this switch
    {
        Confirmed c => c.Classes,
        Rejected r => r.Classes,
        Unsure u => u.Classes,
        _ => default,
    };
}
