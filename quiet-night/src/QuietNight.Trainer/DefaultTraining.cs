using System.Diagnostics;
using QuietNight.Core.Detection;
using QuietNight.Core.Training;

namespace QuietNight.Trainer;

/// <summary>
/// Trains the bundled heads from the training split of the reference sounds. Each clip is laid over a training room
/// at several loudness levels; the validation split reports honest accuracies on clips the heads never saw.
/// </summary>
internal static class DefaultTraining
{
    private static readonly float[] TargetLevels = [8, 14, 20, 26];
    private static readonly float[] OtherLevels = [6, 14, 22];
    private const int FrameReplayPerClass = 3_000;
    private const int SoundReplayPerClass = 200;

    public static readonly TrainingOptions FrameOptions = new(Epochs: 8, LearningRate: 0.003f, L2: 1, Seed: 1);
    public static readonly TrainingOptions SoundOptions = new(Epochs: 20, LearningRate: 0.003f, L2: 3, Seed: 2);

    public static void Run(IReadOnlyList<ReferenceSound> manifest, string audio, string modelPath, string output, int threads)
    {
        var clock = Stopwatch.StartNew();
        var usable = manifest.Where(s => !s.IsTest && File.Exists(ReferenceAudio.Mp3(audio, s))).ToList();
        var beds = usable.Where(s => s.IsBackground && s.IsTraining)
            .Select(s => RoomBed.Loop(ReferenceAudio.Loudest(ReferenceAudio.Decode(ReferenceAudio.Mp3(audio, s)), ReferenceAudio.BackgroundLength)))
            .Where(b => b.Length >= 16_000)
            .Append(RoomBed.Create())
            .ToList();
        var random = new Random(20261105);
        var clips = usable.Where(s => !s.IsBackground).ToList();
        Console.WriteLine($"{clips.Count(s => s.IsTraining)} training clips, {beds.Count} rooms, {clips.Count(s => s.IsValidation)} validation clips");

        using var model = new SoundModel(File.ReadAllBytes(modelPath), threads);
        var builder = new ExampleBuilder(model, ListenerSettings.Default);
        var frames = new LabeledSet(FrameFeatures.Count);
        var sounds = new LabeledSet(SoundRecognizer.FeatureCount);
        var validationFrames = new LabeledSet(FrameFeatures.Count);
        var validationSounds = new LabeledSet(SoundRecognizer.FeatureCount);
        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            var sound = ReferenceAudio.Loudest(ReferenceAudio.Decode(ReferenceAudio.Mp3(audio, clip)), ReferenceAudio.TrainingLength);
            var held = clip.IsValidation;
            foreach (var level in clip.Noise.IsTarget() ? TargetLevels : OtherLevels)
            {
                var bed = beds[random.Next(beds.Count)];
                builder.Add(bed, sound, clip.Noise, level, held ? validationFrames : frames, held ? validationSounds : sounds);
            }
            if ((i + 1) % 25 == 0)
                Console.WriteLine($"  {i + 1}/{clips.Count} clips, {frames.Count} hops, {sounds.Count} windows, {clock.Elapsed:mm\\:ss}");
        }

        if (Environment.GetEnvironmentVariable("QUIET_NIGHT_DUMP") is { Length: > 0 } dump)
        {
            Directory.CreateDirectory(dump);
            Save(Path.Combine(dump, "frames-train.bin"), frames);
            Save(Path.Combine(dump, "frames-validation.bin"), validationFrames);
            Save(Path.Combine(dump, "sounds-train.bin"), sounds);
            Save(Path.Combine(dump, "sounds-validation.bin"), validationSounds);
        }
        var frameHead = SoftmaxTrainer.Fit(frames, FrameOptions);
        var soundHead = SoftmaxTrainer.Fit(sounds, SoundOptions);
        Report("fast detector", frameHead, frames, validationFrames);
        Report("sound model head", soundHead, sounds, validationSounds);

        new DetectorHeads(frameHead, soundHead).Save(output);
        Save(Path.Combine(output, DetectorHeads.FrameReplayFile), frames.Sample(FrameReplayPerClass, seed: 3));
        Save(Path.Combine(output, DetectorHeads.SoundReplayFile), sounds.Sample(SoundReplayPerClass, seed: 4));
        Console.WriteLine($"Wrote heads and replay examples to {output} in {clock.Elapsed:mm\\:ss}");
    }

    private static void Report(string name, SoftmaxModel head, LabeledSet training, LabeledSet validation)
    {
        Console.WriteLine($"{name}: training balanced accuracy {SoftmaxTrainer.BalancedAccuracy(head, training):P1}, validation clips {SoftmaxTrainer.BalancedAccuracy(head, validation):P1}");
        foreach (var noise in Enum.GetValues<NoiseClass>())
        {
            var rows = Enumerable.Range(0, validation.Count).Where(r => validation.Label(r) == noise).ToArray();
            if (rows.Length == 0) continue;
            var correct = rows.Count(r => head.Classify(validation.Row(r)) == noise);
            Console.WriteLine($"  {noise,-10} {correct,6}/{rows.Length,-6} {(double)correct / rows.Length:P1}");
        }
    }

    private static void Save(string path, LabeledSet set)
    {
        using var stream = File.Create(path);
        set.Write(stream);
        Console.WriteLine($"  {Path.GetFileName(path)}: {set.Count} examples");
    }
}
