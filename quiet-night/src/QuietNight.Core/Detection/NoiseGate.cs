using QuietNight.Core.Analysis;

namespace QuietNight.Core.Detection;

/// <summary>
/// Decides whether the current sound is masked. A sound that needs masking opens the gate at once, unless the fast
/// detector is fairly sure it is not a target, so the masker reacts within a hop or two. While the sound lasts, every
/// sound-model verdict adds its log-odds to the episode's evidence: once the evidence reaches
/// <see cref="ConfirmEvidence"/> the sound is a confirmed target and later sounds within
/// <see cref="ListenerSettings.ConfirmedSeconds"/> are masked as part of the same run; once it falls to
/// <see cref="RejectEvidence"/> the sound is left alone until it ends, and sounds in the next
/// <see cref="RejectedRunSeconds"/>, such as the next chirp of a cricket, start out rejected too.
/// </summary>
public sealed class NoiseGate(ListenerSettings settings)
{
    /// <summary>Evidence, in summed log-odds, that confirms a target: two clear windows, or one very clear one.</summary>
    public const float ConfirmEvidence = 2.5f;

    /// <summary>Evidence that rejects a sound as something that must stay audible or is harmless.</summary>
    public const float RejectEvidence = -2.5f;

    /// <summary>The most a single window can add or take away.</summary>
    public const float MaxWindowEvidence = 3f;

    /// <summary>The head start a sound gets when it follows a confirmed run.</summary>
    public const float RunPrior = 1f;

    /// <summary>How long after a rejected sound new sounds start out rejected.</summary>
    public const float RejectedRunSeconds = 4;

    /// <summary>Where a sound starts when it follows a rejected one: rejected, but one target-like window reopens the gate.</summary>
    public const float RejectedRunPrior = RejectEvidence - 0.5f;

    private static readonly int EndAfterHops = ListeningFormat.Hops(TimeSpan.FromSeconds(1));

    private readonly int runHops = ListeningFormat.Hops(TimeSpan.FromSeconds(settings.ConfirmedSeconds));
    private readonly int rejectedRunHops = ListeningFormat.Hops(TimeSpan.FromSeconds(RejectedRunSeconds));
    private long rejectedRunUntil = -1;
    private readonly float[] classEvidence = new float[NoiseClasses.Count];
    private long hop;
    private int agreeing;
    private long lastNeeded = long.MinValue / 2;
    private long runUntil = -1;
    private bool inEpisode;
    private float evidence;

    public bool Open { get; private set; }

    /// <summary>The noise being masked, or <see cref="NoiseClass.Other"/> when the gate is closed.</summary>
    public NoiseClass Noise { get; private set; }

    /// <summary>True while the current sound is a confirmed target or follows one closely.</summary>
    public bool Confirming => (inEpisode && evidence >= ConfirmEvidence) || hop < runUntil;

    /// <summary>True when the sound model has ruled the current sound out.</summary>
    public bool Rejecting => inEpisode && evidence <= RejectEvidence;

    /// <summary>The summed log-odds of the current sound being a target.</summary>
    public float Evidence => evidence;

    /// <param name="targetProbability">The fast detector's probability that the hop holds a target noise.</param>
    /// <param name="likely">The fast detector's most likely target.</param>
    /// <param name="needed">Whether the hop holds a sound louder than the bedtime masker covers.</param>
    public void Hop(float targetProbability, NoiseClass likely, bool needed)
    {
        if (needed) lastNeeded = hop;
        if (inEpisode && hop - lastNeeded > EndAfterHops)
        {
            if (evidence >= ConfirmEvidence) runUntil = hop + runHops;
            if (evidence <= RejectEvidence) rejectedRunUntil = hop + rejectedRunHops;
            inEpisode = false;
        }
        if (needed && !inEpisode)
        {
            inEpisode = true;
            evidence = hop < runUntil ? RunPrior : hop < rejectedRunUntil ? RejectedRunPrior : 0;
            if (hop >= runUntil) Array.Clear(classEvidence);
        }
        agreeing = needed && targetProbability >= settings.ProvisionalProbability ? agreeing + 1 : 0;
        var provisional = agreeing >= settings.ProvisionalHops;
        Open = inEpisode && !Rejecting && (Confirming || provisional);
        Noise = Open && Confirming ? BestTarget() : Open ? likely : NoiseClass.Other;
        hop++;
    }

    /// <summary>Adds a sound-model verdict about the current sound.</summary>
    /// <returns>True when the verdict ruled the sound out, so its masking should fall away quickly.</returns>
    public bool Apply(SoundVerdict verdict)
    {
        if (!inEpisode) return false;
        var wasRejecting = Rejecting;
        var p = Math.Clamp(verdict.TargetProbability, 1e-4f, 1 - 1e-4f);
        evidence = Math.Clamp(evidence + Math.Clamp(MathF.Log(p / (1 - p)), -MaxWindowEvidence, MaxWindowEvidence),
            RejectEvidence - MaxWindowEvidence, ConfirmEvidence + MaxWindowEvidence);
        // Only windows that look like a target say which target it is; the share each target takes of the target
        // probability is what counts, so quieter windows do not drown out clear ones.
        if (verdict.TargetProbability >= 0.5f)
        {
            var classes = verdict.Classes;
            foreach (var target in NoiseClasses.Targets)
                classEvidence[(int)target] += MathF.Log(MathF.Max(classes[target] / verdict.TargetProbability, 1e-6f));
        }
        return Rejecting && !wasRejecting;
    }

    private NoiseClass BestTarget()
    {
        var best = NoiseClass.Cat;
        foreach (var target in NoiseClasses.Targets)
            if (classEvidence[(int)target] > classEvidence[(int)best])
                best = target;
        return best;
    }
}
