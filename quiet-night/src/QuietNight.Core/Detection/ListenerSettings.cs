using QuietNight.Core.Masking;

namespace QuietNight.Core.Detection;

/// <summary>
/// How the two detection stages decide when a sound is a disturbing noise.
/// </summary>
/// <param name="ActivityDb">A band this far above the room counts as something happening.</param>
/// <param name="ProvisionalProbability">
/// A sound that stands out opens the gate at once unless the fast detector puts its target probability below this.
/// The fast detector is weak on single hops, so it only vetoes sounds it is fairly sure about.
/// </param>
/// <param name="ProvisionalHops">Consecutive 10 ms hops the fast detector must agree for.</param>
/// <param name="ConfirmProbability">A single window at or above this probability is reported as a likely target.</param>
/// <param name="RejectProbability">A single window below this probability is reported as no target.</param>
/// <param name="ConfirmedSeconds">After a confirmed sound ends, new sounds join its run for this long; cats and traffic come in runs.</param>
/// <param name="ClassifyEverySeconds">How often the sound model runs while something needs masking.</param>
/// <param name="ConfirmEvidence">Summed, weighted log-odds that confirm the current sound as a target.</param>
/// <param name="RejectEvidence">Summed, weighted log-odds that rule the current sound out.</param>
/// <param name="WindowWeight">
/// How much each window's log-odds count. Windows of 0.96 s every 0.24 s overlap by three quarters, so their verdicts
/// are far from independent.
/// </param>
/// <param name="RunPrior">The head start a sound gets when it follows a confirmed run.</param>
/// <param name="RejectedRunSeconds">How long after a rejected sound new sounds that sound like it are rejected too, like the next knock on a door.</param>
/// <param name="SameSoundSimilarity">How alike, by the cosine of their rise above the room, two sounds must be to belong to one run.</param>
/// <param name="FullWeightFill">
/// The share of a window the sound must fill before the window's evidence against a target counts in full; below it,
/// that evidence counts in proportion.
/// </param>
/// <param name="OtherPrior">
/// Log-odds added to <see cref="NoiseClass.Other"/> before a verdict. The head is trained with every class equally
/// common, but at night most sounds that stand out are not targets.
/// </param>
public sealed record ListenerSettings(
    MaskerSettings Masker,
    float ActivityDb = 6,
    float ProvisionalProbability = 0.4f,
    int ProvisionalHops = 2,
    float ConfirmProbability = 0.7f,
    float RejectProbability = 0.35f,
    float ConfirmedSeconds = 8,
    float ClassifyEverySeconds = 0.24f,
    float ConfirmEvidence = 2f,
    float RejectEvidence = -2f,
    float WindowWeight = 1f,
    float RunPrior = 1f,
    float RejectedRunSeconds = 4,
    float SameSoundSimilarity = 0.6f,
    float OtherPrior = 0,
    float FullWeightFill = 0.5f)
{
    public static ListenerSettings Default { get; } = new(new MaskerSettings());

    public SoundVerdict Judge(Span<float> probabilities)
    {
        if (OtherPrior != 0)
        {
            var other = probabilities[(int)NoiseClass.Other] * MathF.Exp(OtherPrior);
            var total = other + 1 - probabilities[(int)NoiseClass.Other];
            probabilities[(int)NoiseClass.Other] = other / total;
            for (var c = 1; c < probabilities.Length; c++)
                probabilities[c] /= total;
        }
        var target = NoiseClasses.TargetProbability(probabilities);
        var likely = NoiseClasses.BestTarget(probabilities);
        var classes = ClassProbabilities.From(probabilities);
        if (target >= ConfirmProbability) return new Confirmed(likely, target, classes);
        if (target < RejectProbability) return new Rejected(target, classes);
        return new Unsure(likely, target, classes);
    }
}
