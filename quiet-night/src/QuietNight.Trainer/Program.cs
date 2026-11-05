using QuietNight.Core.Training;
using QuietNight.Trainer;

// Developer tool: prepares the committed test sounds and trains the heads bundled with the app.
//   prepare <split> <manifest.json> <mp3 directory> <output directory>
//   train <manifest.json> <mp3 directory> <sound-model.onnx> <output directory> [threads]
if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: prepare <split> <manifest> <audio> <output> | train <manifest> <audio> <model> <output> [threads]");
    return 2;
}

var prepare = args[0] == "prepare";
IReadOnlyList<ReferenceSound> manifest;
using (var stream = File.OpenRead(args[prepare ? 2 : 1]))
    manifest = ReferenceSound.ReadManifest(stream);

switch (args[0])
{
    case "prepare" when args.Length >= 5:
        var split = args[1];
        foreach (var sound in manifest.Where(s => s.Split == split))
        {
            var source = ReferenceAudio.Mp3(args[3], sound);
            if (!File.Exists(source))
            {
                Console.Error.WriteLine($"Missing {source}");
                return 1;
            }
            var length = sound.IsBackground ? ReferenceAudio.BackgroundLength : ReferenceAudio.EventLength;
            var samples = ReferenceAudio.Loudest(ReferenceAudio.Decode(source), length);
            ReferenceAudio.EncodeOpus(samples, Path.Combine(args[4], sound.Group, $"{sound.Id}.opus"));
        }
        Console.WriteLine($"Encoded {manifest.Count(s => s.Split == split)} {split} sounds into {args[4]}");
        return 0;
    case "train" when args.Length >= 5:
        DefaultTraining.Run(manifest, args[2], args[3], args[4], args.Length > 5 ? int.Parse(args[5]) : Environment.ProcessorCount);
        return 0;
    default:
        Console.Error.WriteLine($"Unknown command {args[0]}");
        return 2;
}
