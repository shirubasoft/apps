using QuietNight.Core.Analysis;
using QuietNight.Core.Detection;

namespace QuietNight.Core.Training;

public sealed record LabeledAudio(float[] Samples, NoiseClass Label);

/// <summary>Accuracies before and after training, measured on parts of the recordings held back from training.</summary>
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
/// replayed reference examples. The new heads are kept only if they do better on the held-back part of the
/// recordings and do not lose more than a few points on the reference sounds.
/// </summary>
public sealed class PersonalTrainer(SoundModel model, ListenerSettings settings)
{
    public const double ValidationShare = 0.2;
    public const double AllowedReplayLoss = 0.03;
    private const int MaxSeconds = 30;
    private const float UserWeight = 3;
    private static readonly float[] Levels = [10, 18, 26];
    private static readonly TrainingOptions FineTuning = new(Epochs: 12, LearningRate: 0.002f, L2: 0, AnchorL2: 3, Seed: 11);

    public (DetectorHeads Heads, TrainingReport Report) Train(
        IReadOnlyList<LabeledAudio> recordings,
        DetectorHeads current,
        LabeledSet frameReplay,
        LabeledSet soundReplay,
        IProgress<double>? progress = null)
    {
        var builder = new ExampleBuilder(model, settings);
        var frames = new LabeledSet(FrameFeatures.Count);
        var sounds = new LabeledSet(SoundRecognizer.FeatureCount);
        var beds = recordings.Where(r => !r.Label.IsTarget())
            .Select(r => (Source: (LabeledAudio?)r, Samples: RoomBed.Loop(Trim(r.Samples))))
            .Where(b => b.Samples.Length >= ListeningFormat.SampleRate)
            .Take(3)
            .Append((Source: (LabeledAudio?)null, Samples: RoomBed.Create()))
            .ToList();
        var step = 0;
        var steps = recordings.Count * Levels.Length;
        foreach (var recording in recordings)
        {
            var sound = Trim(recording.Samples);
            foreach (var level in Levels)
            {
                var bed = beds[step % beds.Count];
                if (ReferenceEquals(bed.Source, recording)) bed = beds[^1];
                builder.Add(bed.Samples, sound, recording.Label, level, frames, sounds);
                progress?.Report(0.6 * ++step / steps);
            }
        }

        var (frameTrain, frameValidation) = frames.Split(ValidationShare, seed: 5);
        var (soundTrain, soundValidation) = sounds.Split(ValidationShare, seed: 6);
        var (frameReplayTrain, frameReplayValidation) = frameReplay.Split(ValidationShare, seed: 7);
        var (soundReplayTrain, soundReplayValidation) = soundReplay.Split(ValidationShare, seed: 8);

        var frameData = new LabeledSet(FrameFeatures.Count);
        frameData.AddRange(frameReplayTrain);
        frameData.AddRange(frameTrain, UserWeight);
        var soundData = new LabeledSet(SoundRecognizer.FeatureCount);
        soundData.AddRange(soundReplayTrain);
        soundData.AddRange(soundTrain, UserWeight);

        var frameHead = SoftmaxTrainer.FineTune(current.Frame, frameData, FineTuning, Scaled(progress, 0.6, 0.15));
        var soundHead = SoftmaxTrainer.FineTune(current.Sound, soundData, FineTuning, Scaled(progress, 0.75, 0.25));

        var replayBefore = Mean(SoftmaxTrainer.BalancedAccuracy(current.Frame, frameReplayValidation), SoftmaxTrainer.BalancedAccuracy(current.Sound, soundReplayValidation));
        var replayAfter = Mean(SoftmaxTrainer.BalancedAccuracy(frameHead, frameReplayValidation), SoftmaxTrainer.BalancedAccuracy(soundHead, soundReplayValidation));
        var report = new TrainingReport(
            recordings.Count,
            frames.Count,
            sounds.Count,
            SoftmaxTrainer.BalancedAccuracy(current.Frame, frameValidation),
            SoftmaxTrainer.BalancedAccuracy(frameHead, frameValidation),
            SoftmaxTrainer.BalancedAccuracy(current.Sound, soundValidation),
            SoftmaxTrainer.BalancedAccuracy(soundHead, soundValidation),
            replayBefore,
            replayAfter,
            Accepted: false);
        var improves = Mean(report.SoundAfter, report.FrameAfter) >= Mean(report.SoundBefore, report.FrameBefore);
        var keepsReference = replayAfter >= replayBefore - AllowedReplayLoss;
        report = report with { Accepted = recordings.Count > 0 && improves && keepsReference };
        progress?.Report(1);
        return (report.Accepted ? new DetectorHeads(frameHead, soundHead) : current, report);
    }

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
