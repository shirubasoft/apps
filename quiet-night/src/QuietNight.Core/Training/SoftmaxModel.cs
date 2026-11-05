using QuietNight.Core.Detection;

namespace QuietNight.Core.Training;

/// <summary>Multinomial logistic regression with its own feature standardisation.</summary>
public sealed class SoftmaxModel
{
    private const uint Magic = 0x4D58_4E51; // "QNXM"
    private const int Version = 1;

    private readonly float[] mean;
    private readonly float[] scale;
    private readonly float[] weights;
    private readonly float[] bias;

    public SoftmaxModel(int classes, float[] mean, float[] scale, float[] weights, float[] bias)
    {
        if (mean.Length != scale.Length || weights.Length != classes * mean.Length || bias.Length != classes)
            throw new ArgumentException("Model dimensions do not agree.");
        Classes = classes;
        Features = mean.Length;
        this.mean = mean;
        this.scale = scale;
        this.weights = weights;
        this.bias = bias;
    }

    public int Classes { get; }

    public int Features { get; }

    internal Span<float> Weights => weights;

    internal Span<float> Bias => bias;

    internal ReadOnlySpan<float> Mean => mean;

    internal ReadOnlySpan<float> Scale => scale;

    public static SoftmaxModel Zero(int classes, float[] mean, float[] scale) =>
        new(classes, mean, scale, new float[classes * mean.Length], new float[classes]);

    public void Predict(ReadOnlySpan<float> features, Span<float> probabilities)
    {
        if (features.Length != Features)
            throw new ArgumentException($"Expected {Features} features but got {features.Length}.", nameof(features));
        var max = float.MinValue;
        for (var c = 0; c < Classes; c++)
        {
            var z = bias[c];
            var row = weights.AsSpan(c * Features, Features);
            for (var d = 0; d < Features; d++)
                z += row[d] * (features[d] - mean[d]) * scale[d];
            probabilities[c] = z;
            max = MathF.Max(max, z);
        }
        var sum = 0f;
        for (var c = 0; c < Classes; c++)
        {
            probabilities[c] = MathF.Exp(probabilities[c] - max);
            sum += probabilities[c];
        }
        for (var c = 0; c < Classes; c++)
            probabilities[c] /= sum;
    }

    public NoiseClass Classify(ReadOnlySpan<float> features)
    {
        Span<float> probabilities = stackalloc float[Classes];
        Predict(features, probabilities);
        var best = 0;
        for (var c = 1; c < Classes; c++)
            if (probabilities[c] > probabilities[best])
                best = c;
        return (NoiseClass)best;
    }

    public SoftmaxModel Clone() => new(Classes, [.. mean], [.. scale], [.. weights], [.. bias]);

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(Classes);
        writer.Write(Features);
        foreach (var array in new[] { mean, scale, weights, bias })
            foreach (var value in array)
                writer.Write(value);
    }

    public static SoftmaxModel Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version)
            throw new InvalidDataException("This is not a Quiet Night model file.");
        var classes = reader.ReadInt32();
        var features = reader.ReadInt32();
        float[] Read(int count)
        {
            var values = new float[count];
            for (var i = 0; i < count; i++)
                values[i] = reader.ReadSingle();
            return values;
        }
        var mean = Read(features);
        var scale = Read(features);
        var weights = Read(classes * features);
        var bias = Read(classes);
        return new SoftmaxModel(classes, mean, scale, weights, bias);
    }
}
