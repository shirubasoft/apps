using QuietNight.Core.Analysis;

namespace QuietNight.Core.Detection;

/// <summary>
/// Decides whether the current sound is masked. A sound that needs masking opens the gate at once, unless the fast
/// detector is fairly sure it is not a target, so the masker reacts within a hop or two. While the sound lasts, every
/// sound-model verdict adds its weighted log-odds to the episode's evidence, except that evidence against a target only
/// counts for the share of the verdict's window the sound filled, since a window taken at a sound's onset mostly holds
/// the quiet room before it, and evidence against a confirmed sound only counts for the share of the window that needed
/// masking, so a pause between meows does not argue the cat out. Once the evidence reaches
/// <see cref="ListenerSettings.ConfirmEvidence"/> the sound is a confirmed target and later sounds within
/// <see cref="ListenerSettings.ConfirmedSeconds"/> that sound like it are masked as part of the same run; once it falls
/// to <see cref="ListenerSettings.RejectEvidence"/> the sound is left alone until it ends, and sounds in the next
/// <see cref="ListenerSettings.RejectedRunSeconds"/> that sound like it, such as the next knock on a door, are rejected
/// too. Whether a new sound sounds like the last one is decided from the shape of its rise above the room over its
/// first <see cref="CompareHops"/> hops, so a cat after a car, or a room's swell after a knock, starts afresh. A rejected
/// sound that grows <see cref="ListenerSettings.NewSoundDb"/> louder than it was when it was rejected, like a motorcycle
/// approaching through a room's swell or a cat calling over the rain, is judged afresh as a new sound.
/// </summary>
public sealed class NoiseGate(ListenerSettings settings)
{
    /// <summary>The most a single window can add or take away, before weighting.</summary>
    public const float MaxWindowEvidence = 3f;

    /// <summary>How much of the previous sound's evidence about which target it was carries into the next sound of a run.</summary>
    public const float ClassCarryOver = 0.5f;

    /// <summary>
    /// How much of the evidence about which target it is survives each new window, so recent windows decide and a long
    /// run of one target cannot outvote a clear new one.
    /// </summary>
    public const float ClassMemory = 0.7f;

    /// <summary>Hops of a new sound the gate hears before deciding whether it continues the last run.</summary>
    public const int CompareHops = 5;

    private static readonly int EndAfterHops = ListeningFormat.Hops(TimeSpan.FromSeconds(1));

    private readonly int runHops = ListeningFormat.Hops(TimeSpan.FromSeconds(settings.ConfirmedSeconds));
    private readonly int rejectedRunHops = ListeningFormat.Hops(TimeSpan.FromSeconds(settings.RejectedRunSeconds));
    private long rejectedRunUntil = -1;
    private long confirmedAt = -1;
    private long rejectedAt = -1;
    private readonly float[] shape = new float[ListeningFormat.MelBands];
    private readonly float[] confirmedShape = new float[ListeningFormat.MelBands];
    private readonly float[] rejectedShape = new float[ListeningFormat.MelBands];
    private readonly float[] runClassEvidence = new float[NoiseClasses.Count];
    private int shapeHops;
    private bool inherited;
    private float episodeLoudestDb;
    private float rejectedAtDb;
    private readonly float[] classEvidence = new float[NoiseClasses.Count];
    private long hop;
    private long episodeStart;
    private int agreeing;
    private long lastNeeded = long.MinValue / 2;
    private long runUntil = -1;
    private bool inEpisode;
    private float evidence;

    public bool Open { get; private set; }

    /// <summary>The noise being masked, or <see cref="NoiseClass.Other"/> when the gate is closed.</summary>
    public NoiseClass Noise { get; private set; }

    /// <summary>
    /// True while the current sound is a confirmed target or continues a confirmed run, and between the sounds of a run.
    /// </summary>
    public bool Confirming => inEpisode ? !Rejecting && (evidence >= settings.ConfirmEvidence || (inherited && hop < runUntil)) : hop < runUntil;

    /// <summary>True for the hop in which the gate ruled the current sound out without a verdict, because it repeats a rejected one.</summary>
    public bool JustRejected { get; private set; }

    /// <summary>True when the sound model has ruled the current sound out.</summary>
    public bool Rejecting => inEpisode && evidence <= settings.RejectEvidence;

    /// <summary>The summed log-odds of the current sound being a target.</summary>
    public float Evidence => evidence;

    /// <summary>Numbers each sound episode, so a verdict about one sound is never applied to the next.</summary>
    public long Episode { get; private set; }

    /// <summary>How many hops the current episode has lasted.</summary>
    public long EpisodeHops => inEpisode ? hop - episodeStart : 0;

    /// <param name="targetProbability">The fast detector's probability that the hop holds a target noise.</param>
    /// <param name="likely">The fast detector's most likely target.</param>
    /// <param name="needed">Whether the hop holds a sound louder than the bedtime masker covers.</param>
    /// <param name="rise">How far each mel band rises above the room, in any fixed unit; empty when unknown.</param>
    /// <param name="loudestDb">How far the loudest band rises above the room, in dB; NaN when unknown.</param>
    public void Hop(float targetProbability, NoiseClass likely, bool needed, ReadOnlySpan<float> rise = default, float loudestDb = float.NaN)
    {
        JustRejected = false;
        if (needed) lastNeeded = hop;
        if (inEpisode && Rejecting && float.IsNegativeInfinity(rejectedAtDb)) rejectedAtDb = episodeLoudestDb;
        if (inEpisode && (hop - lastNeeded > EndAfterHops || (needed && Rejecting && rejectedAtDb > float.NegativeInfinity && loudestDb >= rejectedAtDb + settings.NewSoundDb)))
            EndEpisode();
        if (needed && !inEpisode)
        {
            inEpisode = true;
            Episode++;
            episodeStart = hop;
            // A new sound starts with no evidence and no name; it takes on the run's only once it sounds like it.
            evidence = 0;
            inherited = false;
            episodeLoudestDb = float.NegativeInfinity;
            rejectedAtDb = float.NegativeInfinity;
            shapeHops = 0;
            Array.Clear(shape);
            Array.Clear(classEvidence);
        }
        if (inEpisode && needed && !float.IsNaN(loudestDb))
            episodeLoudestDb = MathF.Max(episodeLoudestDb, loudestDb);
        // The shape of a sound's onset is what runs remember and compare, so like is compared with like.
        if (inEpisode && needed && rise.Length == shape.Length && shapeHops < CompareHops)
        {
            for (var m = 0; m < shape.Length; m++)
                shape[m] += MathF.Max(0, rise[m]);
            if (++shapeHops == CompareHops) Compare();
        }
        agreeing = needed && targetProbability >= settings.ProvisionalProbability ? agreeing + 1 : 0;
        // After a rejected sound, a new one is only masked once it is clear it is not the same sound again.
        var comparing = hop < rejectedRunUntil && shapeHops < CompareHops && rise.Length == shape.Length;
        var provisional = agreeing >= settings.ProvisionalHops && !comparing;
        Open = inEpisode && !Rejecting && (Confirming || provisional);
        Noise = Open && Confirming ? BestTarget() : Open ? likely : NoiseClass.Other;
        hop++;
    }

    /// <summary>Adds a sound-model verdict about the window taken during <paramref name="episode"/>.</summary>
    /// <param name="verdict">The sound model's verdict.</param>
    /// <param name="episode">The episode the window was taken in.</param>
    /// <param name="filled">The share of the window, from 0 to 1, taken after the episode began.</param>
    /// <param name="occupied">The share of the window's hops, from 0 to 1, in which a sound needed masking.</param>
    /// <returns>True when the verdict ruled the sound out, so its masking should fall away quickly.</returns>
    public bool Apply(SoundVerdict verdict, long episode, float filled = 1, float occupied = 1)
    {
        if (!inEpisode || episode != Episode) return false;
        var wasRejecting = Rejecting;
        var p = Math.Clamp(verdict.TargetProbability, 1e-4f, 1 - 1e-4f);
        var logOdds = Math.Clamp(MathF.Log(p / (1 - p)), -MaxWindowEvidence, MaxWindowEvidence);
        if (logOdds < 0) logOdds *= Math.Clamp(filled / settings.FullWeightFill, 0, 1);
        // A confirmed sound is only argued out by windows that hold sound, not by its own pauses.
        if (logOdds < 0 && Confirming) logOdds *= Math.Clamp(occupied / settings.FullWeightFill, 0, 1);
        evidence = Math.Clamp(evidence + settings.WindowWeight * logOdds,
            settings.RejectEvidence - MaxWindowEvidence, settings.ConfirmEvidence + MaxWindowEvidence);
        // Only windows that look like a target say which target it is; the share each target takes of the target
        // probability is what counts, so quieter windows do not drown out clear ones.
        if (verdict.TargetProbability >= 0.5f)
        {
            var classes = verdict.Classes;
            foreach (var target in NoiseClasses.Targets)
                classEvidence[(int)target] = ClassMemory * classEvidence[(int)target] +
                    MathF.Log(MathF.Max(classes[target] / verdict.TargetProbability, 1e-6f));
        }
        if (!Rejecting || wasRejecting) return false;
        // A sound of a confirmed run that the sound model rules out ends the run.
        if (inherited) runUntil = Math.Min(runUntil, hop);
        inherited = false;
        return true;
    }

    private void EndEpisode()
    {
        if (Confirming)
        {
            runUntil = hop + runHops;
            confirmedAt = hop;
            classEvidence.CopyTo(runClassEvidence);
            if (!inherited) shape.CopyTo(confirmedShape);
        }
        if (Rejecting)
        {
            rejectedRunUntil = hop + rejectedRunHops;
            rejectedAt = hop;
            shape.CopyTo(rejectedShape);
        }
        inEpisode = false;
    }

    /// <summary>
    /// Decides whether the new sound continues the confirmed or rejected run before it. When it sounds like both, the
    /// more recent one decides.
    /// </summary>
    private void Compare()
    {
        // A run never overrides the sound model: a sound it has already ruled out stays ruled out unless later windows
        // argue it back.
        var continuesRun = !Rejecting && hop < runUntil && Similarity(shape, confirmedShape) >= settings.SameSoundSimilarity;
        var repeatsRejected = hop < rejectedRunUntil && Similarity(shape, rejectedShape) >= settings.SameSoundSimilarity;
        if (continuesRun && repeatsRejected)
            (continuesRun, repeatsRejected) = confirmedAt >= rejectedAt ? (true, false) : (false, true);
        if (continuesRun)
        {
            inherited = true;
            evidence += settings.RunPrior;
            // Which target it is decays from sound to sound within a run, so a cat after a car is called a cat.
            for (var c = 0; c < classEvidence.Length; c++)
                classEvidence[c] += runClassEvidence[c] * ClassCarryOver;
        }
        else if (repeatsRejected && evidence < settings.ConfirmEvidence)
        {
            JustRejected = !Rejecting;
            evidence = MathF.Min(evidence, settings.RejectEvidence);
        }
    }

    /// <summary>The correlation of two rise shapes across the bands, 0 when either is flat.</summary>
    private static float Similarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float meanA = 0, meanB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            meanA += a[i] / a.Length;
            meanB += b[i] / b.Length;
        }
        float dot = 0, aa = 0, bb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (a[i] - meanA) * (b[i] - meanB);
            aa += (a[i] - meanA) * (a[i] - meanA);
            bb += (b[i] - meanB) * (b[i] - meanB);
        }
        return aa <= 0 || bb <= 0 ? 0 : dot / MathF.Sqrt(aa * bb);
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
