using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;

namespace QuietNight.Core.Training;

public sealed record LabeledAudio(float[] Samples, NoiseClass Label);

/// <summary>
/// Reference examples bundled with the app: some replayed during fine-tuning, others held out from clips the bundled
/// heads never trained on.
/// </summary>
public sealed record ReferenceExamples(LabeledSet Frames, LabeledSet Sounds, LabeledSet HeldOutFrames, LabeledSet HeldOutSounds);

/// <summary>Accuracies before and after training, measured on the held-back end of each recording.</summary>
public sealed record TrainingReport(
    int Recordings,
    int FrameExamples,
    int SoundExamples,
    double FrameBefore,
    double FrameAfter,
    double SoundBefore,
    double SoundAfter,
    double ReplayBefore,
    double ReplayAfter,
    bool Accepted);

/// <summary>
/// Fine-tunes both heads on the user's recordings, mixed into their room at several loudness levels, together with
/// replayed reference examples. The new heads are kept only if the sound model head does at least two points better on
/// the held-back end of the recordings, and neither head loses more than a few points there or on reference sounds
/// they never trained on.
/// </summary>
public sealed class PersonalTrainer(SoundModel model, ListenerSettings settings)
{
    public const double ValidationShare = 0.2;
    public const double AllowedReplayLoss = 0.03;
    public const double AllowedPersonalLoss = 0.02;
    public const double RequiredGain = 0.02;
    private const int MaxSeconds = 30;
    private const float UserWeight = 3;
    private static readonly float[] Levels = [10, 18, 26];
    private static readonly TrainingOptions FineTuning = new(Epochs: 12, LearningRate: 0.002f, L2: 0, AnchorL2: 3, Seed: 11);

    /// <param name="recordings">The user's labelled recordings.</param>
    /// <param name="current">The heads in use now.</param>
    /// <param name="reference">
    /// Replayed reference examples to train with, and held-out reference examples, from clips the bundled heads never
    /// saw, to check that nothing was forgotten.
    /// </param>
    /// <param name="progress">Receives progress from 0 to 1.</param>
    public (DetectorHeads Heads, TrainingReport Report) Train(
        IReadOnlyList<LabeledAudio> recordings,
        DetectorHeads current,
        ReferenceExamples reference,
        IProgress<double>? progress = null)
    {
        var builder = new ExampleBuilder(model, settings);
        var frameTrain = new LabeledSet(FrameFeatures.Count);
        var soundTrain = new LabeledSet(SoundRecognizer.FeatureCount);
        var frameValidation = new LabeledSet(FrameFeatures.Count);
        var soundValidation = new LabeledSet(SoundRecognizer.FeatureCount);
        // The last fifth of each recording is held back before anything is mixed, rooms included, so no overlapping
        // window, alternative mix or background of the same moment lands on both sides.
        var parts = recordings.Select(r =>
        {
            var sound = Trim(r.Samples);
            var held = (int)(sound.Length * ValidationShare);
            return held >= ListeningFormat.SampleRate
                ? (Recording: r, Training: sound[..^held], Validation: sound[^held..])
                : (Recording: r, Training: sound, Validation: Array.Empty<float>());
        }).ToList();
        var beds = parts.Where(p => !p.Recording.Label.IsTarget())
            .Select(p => (Source: (LabeledAudio?)p.Recording, Samples: RoomBed.Loop(p.Training)))
            .Where(b => b.Samples.Length >= ListeningFormat.SampleRate)
            .Take(3)
            .Append((Source: (LabeledAudio?)null, Samples: RoomBed.Create()))
            .ToList();
        var step = 0;
        var steps = recordings.Count * (Levels.Length + 1);
        foreach (var (recording, training, validation) in parts)
        {
            var bed = beds[step % beds.Count];
            if (ReferenceEquals(bed.Source, recording)) bed = beds[^1];
            foreach (var level in Levels)
            {
                builder.Add(bed.Samples, training, recording.Label, level, frameTrain, soundTrain);
                progress?.Report(0.6 * ++step / steps);
            }
            if (validation.Length > 0)
                builder.Add(bed.Samples, validation, recording.Label, Levels[Levels.Length / 2], frameValidation, soundValidation);
            progress?.Report(0.6 * ++step / steps);
        }

        var frameData = new LabeledSet(FrameFeatures.Count);
        frameData.AddRange(reference.Frames);
        frameData.AddRange(frameTrain, UserWeight);
        var soundData = new LabeledSet(SoundRecognizer.FeatureCount);
        soundData.AddRange(reference.Sounds);
        soundData.AddRange(soundTrain, UserWeight);
        var frameReplayValidation = reference.HeldOutFrames;
        var soundReplayValidation = reference.HeldOutSounds;

        var frameHead = SoftmaxTrainer.FineTune(current.Frame, frameData, FineTuning, Scaled(progress, 0.6, 0.15));
        var soundHead = SoftmaxTrainer.FineTune(current.Sound, soundData, FineTuning, Scaled(progress, 0.75, 0.25));

        var replayBefore = Mean(SoftmaxTrainer.BalancedAccuracy(current.Frame, frameReplayValidation), SoftmaxTrainer.BalancedAccuracy(current.Sound, soundReplayValidation));
        var replayAfter = Mean(SoftmaxTrainer.BalancedAccuracy(frameHead, frameReplayValidation), SoftmaxTrainer.BalancedAccuracy(soundHead, soundReplayValidation));
        var report = new TrainingReport(
            recordings.Count,
            frameTrain.Count,
            soundTrain.Count,
            SoftmaxTrainer.BalancedAccuracy(current.Frame, frameValidation),
            SoftmaxTrainer.BalancedAccuracy(frameHead, frameValidation),
            SoftmaxTrainer.BalancedAccuracy(current.Sound, soundValidation),
            SoftmaxTrainer.BalancedAccuracy(soundHead, soundValidation),
            replayBefore,
            replayAfter,
            Accepted: false);
        // The sound model head must do measurably better on the held-back recordings, and neither head may get worse on
        // them or on the reference sounds, so one head's gain cannot hide the other's loss.
        var improves = report.SoundAfter >= report.SoundBefore + RequiredGain && !Worse(report.FrameAfter, report.FrameBefore, AllowedPersonalLoss);
        var keepsReference =
            !Worse(SoftmaxTrainer.BalancedAccuracy(frameHead, frameReplayValidation), SoftmaxTrainer.BalancedAccuracy(current.Frame, frameReplayValidation), AllowedReplayLoss) &&
            !Worse(SoftmaxTrainer.BalancedAccuracy(soundHead, soundReplayValidation), SoftmaxTrainer.BalancedAccuracy(current.Sound, soundReplayValidation), AllowedReplayLoss);
        report = report with { Accepted = recordings.Count > 0 && improves && keepsReference };
        progress?.Report(1);
        return (report.Accepted ? new DetectorHeads(frameHead, soundHead) : current, report);
    }

    private static bool Worse(double after, double before, double allowed) =>
        !double.IsNaN(after) && !double.IsNaN(before) && after < before - allowed;

    private static float[] Trim(float[] samples) =>
        samples.Length <= MaxSeconds * ListeningFormat.SampleRate ? samples : samples[..(MaxSeconds * ListeningFormat.SampleRate)];

    private static double Mean(double a, double b) => (double.IsNaN(a), double.IsNaN(b)) switch
    {
        (true, true) => double.NaN,
        (true, false) => b,
        (false, true) => a,
        _ => (a + b) / 2,
    };

    private static ScaledProgress? Scaled(IProgress<double>? progress, double start, double span) =>
        progress is null ? null : new ScaledProgress(progress, start, span);

    private sealed class ScaledProgress(IProgress<double> inner, double start, double span) : IProgress<double>
    {
        public void Report(double value) => inner.Report(start + span * value);
    }
}
