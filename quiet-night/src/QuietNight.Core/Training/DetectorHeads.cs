namespace QuietNight.Core.Training;

/// <summary>
/// The two trainable heads: one for the fast per-hop detector and one on top of YAMNet's embeddings, plus the replay
/// examples that keep fine-tuning from forgetting the reference sounds.
/// </summary>
public sealed record DetectorHeads(SoftmaxModel Frame, SoftmaxModel Sound)
{
    public const string FrameFile = "frame-head.bin";
    public const string SoundFile = "sound-head.bin";
    public const string FrameReplayFile = "frame-replay.bin";
    public const string SoundReplayFile = "sound-replay.bin";

    public static DetectorHeads Load(Func<string, Stream> open)
    {
        using var frame = open(FrameFile);
        using var sound = open(SoundFile);
        return new DetectorHeads(SoftmaxModel.Read(frame), SoftmaxModel.Read(sound));
    }

    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        Write(Path.Combine(directory, FrameFile), Frame.Write);
        Write(Path.Combine(directory, SoundFile), Sound.Write);
    }

    public static (LabeledSet Frames, LabeledSet Sounds) LoadReplay(Func<string, Stream> open)
    {
        using var frames = open(FrameReplayFile);
        using var sounds = open(SoundReplayFile);
        return (LabeledSet.Read(frames), LabeledSet.Read(sounds));
    }

    private static void Write(string path, Action<Stream> write)
    {
        var temporary = path + ".part";
        using (var stream = File.Create(temporary))
            write(stream);
        File.Move(temporary, path, overwrite: true);
    }
}
