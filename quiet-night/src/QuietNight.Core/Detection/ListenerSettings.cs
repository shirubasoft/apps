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
public sealed record ListenerSettings(
    MaskerSettings Masker,
    float ActivityDb = 6,
    float ProvisionalProbability = 0.3f,
    int ProvisionalHops = 2,
    float ConfirmProbability = 0.7f,
    float RejectProbability = 0.35f,
    float ConfirmedSeconds = 8,
    float ClassifyEverySeconds = 0.24f)
{
    public static ListenerSettings Default { get; } = new(new MaskerSettings());

    public SoundVerdict Judge(ReadOnlySpan<float> probabilities)
    {
        var target = NoiseClasses.TargetProbability(probabilities);
        var likely = NoiseClasses.BestTarget(probabilities);
        var classes = ClassProbabilities.From(probabilities);
        if (target >= ConfirmProbability) return new Confirmed(likely, target, classes);
        if (target < RejectProbability) return new Rejected(target, classes);
        return new Unsure(likely, target, classes);
    }
}
