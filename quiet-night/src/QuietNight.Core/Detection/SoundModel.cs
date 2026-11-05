using Microsoft.ML.OnnxRuntime;

namespace QuietNight.Core.Detection;

/// <summary>
/// Google's prebuilt YAMNet audio event model (AudioSet, Apache-2.0), run with ONNX Runtime. It turns 0.96 s of
/// 16 kHz audio into a 1024-value embedding that the trainable <see cref="SoundRecognizer"/> head classifies.
/// </summary>
public sealed class SoundModel : IDisposable
{
    public const int WindowSamples = 15_360;
    public const int EmbeddingSize = 1_024;
    public const int AudioSetClasses = 521;

    /// <summary>The embedding followed by the log-odds of YAMNet's own AudioSet classes.</summary>
    public const int FeatureCount = EmbeddingSize + AudioSetClasses;

    private readonly InferenceSession session;
    private readonly float[] input = new float[WindowSamples];
    private readonly OrtValue inputValue;
    private readonly RunOptions run = new();
    private readonly string[] inputNames = ["waveform"];
    private readonly string[] outputNames = ["output_0", "output_1"];

    public SoundModel(byte[] onnx, int threads = 1)
    {
        using var options = new SessionOptions
        {
            IntraOpNumThreads = threads,
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };
        session = new InferenceSession(onnx, options);
        inputValue = OrtValue.CreateTensorValueFromMemory(input, [WindowSamples]);
    }

    /// <summary>Writes the window's embedding and, optionally, YAMNet's own AudioSet scores.</summary>
    public void Embed(ReadOnlySpan<float> window, Span<float> embedding, Span<float> audioSetScores = default)
    {
        if (window.Length != WindowSamples)
            throw new ArgumentException($"A window holds {WindowSamples} samples.", nameof(window));
        window.CopyTo(input);
        using var outputs = session.Run(run, inputNames, [inputValue], outputNames);
        outputs[1].GetTensorDataAsSpan<float>()[..EmbeddingSize].CopyTo(embedding);
        if (!audioSetScores.IsEmpty)
            outputs[0].GetTensorDataAsSpan<float>()[..AudioSetClasses].CopyTo(audioSetScores);
    }

    /// <summary>
    /// Writes the window's embedding and the log-odds of YAMNet's AudioSet classes, so a small head can lean on the
    /// classifier Google trained on two million clips as well as on the raw embedding.
    /// </summary>
    public void Features(ReadOnlySpan<float> window, Span<float> features)
    {
        Embed(window, features[..EmbeddingSize], features[EmbeddingSize..FeatureCount]);
        foreach (ref var score in features[EmbeddingSize..FeatureCount])
        {
            var p = Math.Clamp(score, 1e-4f, 1 - 1e-4f);
            score = MathF.Log(p / (1 - p));
        }
    }

    public void Dispose()
    {
        inputValue.Dispose();
        run.Dispose();
        session.Dispose();
    }
}
