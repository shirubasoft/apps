namespace QuietNight.Core.Detection;

/// <summary>
/// Target noises the user has chosen not to mask. The sound model counts them as <see cref="NoiseClass.Other"/>, so once
/// it has heard one the masker lets it be like any other harmless sound. The fast detector still reacts to every target
/// at onset: on its own it cannot tell a cat from an engine within the first hops.
/// </summary>
public readonly record struct IgnoredNoises(bool Cats = false, bool Motorcycles = false, bool Cars = false)
{
    /// <summary>True when every target is ignored, so nothing would ever be masked.</summary>
    public bool All => Cats && Motorcycles && Cars;

    public bool Contains(NoiseClass noise) => noise switch
    {
        NoiseClass.Cat => Cats,
        NoiseClass.Motorcycle => Motorcycles,
        NoiseClass.Car => Cars,
        _ => false,
    };

    public IgnoredNoises With(NoiseClass noise, bool ignored) => noise switch
    {
        NoiseClass.Cat => this with { Cats = ignored },
        NoiseClass.Motorcycle => this with { Motorcycles = ignored },
        NoiseClass.Car => this with { Cars = ignored },
        _ => this,
    };

    /// <summary>The most likely target among those not ignored.</summary>
    public NoiseClass BestTarget(ReadOnlySpan<float> probabilities)
    {
        NoiseClass? best = null;
        foreach (var target in NoiseClasses.Targets)
            if (!Contains(target) && (best is not { } current || probabilities[(int)target] > probabilities[(int)current]))
                best = target;
        return best ?? NoiseClass.Cat;
    }

    /// <summary>Moves the probability of every ignored target to <see cref="NoiseClass.Other"/>.</summary>
    public void Fold(Span<float> probabilities)
    {
        foreach (var target in NoiseClasses.Targets)
        {
            if (!Contains(target)) continue;
            probabilities[(int)NoiseClass.Other] += probabilities[(int)target];
            probabilities[(int)target] = 0;
        }
    }
}
