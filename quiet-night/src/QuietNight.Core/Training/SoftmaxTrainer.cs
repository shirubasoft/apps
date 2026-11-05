using QuietNight.Core.Detection;

namespace QuietNight.Core.Training;

/// <param name="L2">Decoupled weight decay towards zero, as in AdamW.</param>
/// <param name="AnchorL2">Decoupled decay towards the starting model's weights when fine-tuning.</param>
public sealed record TrainingOptions(
    int Epochs = 20,
    int BatchSize = 64,
    float LearningRate = 0.003f,
    float L2 = 1,
    float AnchorL2 = 0,
    int Seed = 1);

/// <summary>
/// Fits <see cref="SoftmaxModel"/> with Adam on class-balanced, weighted mini-batches. The learning rate decays on a
/// cosine to zero so the last epochs settle instead of bouncing around the optimum, and weight decay is applied apart
/// from the adaptive step (AdamW), so it regularises every weight equally.
/// </summary>
public static class SoftmaxTrainer
{
    private const float Beta1 = 0.9f;
    private const float Beta2 = 0.999f;
    private const float Epsilon = 1e-8f;

    /// <summary>Trains a new model whose standardisation comes from <paramref name="data"/>.</summary>
    public static SoftmaxModel Fit(LabeledSet data, TrainingOptions options, IProgress<double>? progress = null)
    {
        var (mean, scale) = Standardisation(data);
        var model = SoftmaxModel.Zero(NoiseClasses.Count, mean, scale);
        Optimise(model, null, data, options, progress);
        return model;
    }

    /// <summary>
    /// Continues training from <paramref name="start"/>, keeping its standardisation and pulling the weights back
    /// towards it with <see cref="TrainingOptions.AnchorL2"/> so a few recordings cannot erase what it knew.
    /// </summary>
    public static SoftmaxModel FineTune(SoftmaxModel start, LabeledSet data, TrainingOptions options, IProgress<double>? progress = null)
    {
        var model = start.Clone();
        Optimise(model, start, data, options, progress);
        return model;
    }

    public static double Accuracy(SoftmaxModel model, LabeledSet data)
    {
        if (data.Count == 0) return double.NaN;
        var correct = 0;
        for (var i = 0; i < data.Count; i++)
            if (model.Classify(data.Row(i)) == data.Label(i))
                correct++;
        return (double)correct / data.Count;
    }

    /// <summary>Mean of per-class accuracies, so a common class cannot hide a weak one.</summary>
    public static double BalancedAccuracy(SoftmaxModel model, LabeledSet data)
    {
        var correct = new int[NoiseClasses.Count];
        var total = new int[NoiseClasses.Count];
        for (var i = 0; i < data.Count; i++)
        {
            var label = (int)data.Label(i);
            total[label]++;
            if ((int)model.Classify(data.Row(i)) == label)
                correct[label]++;
        }
        var present = Enumerable.Range(0, total.Length).Where(c => total[c] > 0).ToArray();
        return present.Length == 0 ? double.NaN : present.Average(c => (double)correct[c] / total[c]);
    }

    private static (float[] Mean, float[] Scale) Standardisation(LabeledSet data)
    {
        var mean = new double[data.Features];
        var square = new double[data.Features];
        for (var i = 0; i < data.Count; i++)
        {
            var row = data.Row(i);
            for (var d = 0; d < row.Length; d++)
            {
                mean[d] += row[d];
                square[d] += (double)row[d] * row[d];
            }
        }
        var n = Math.Max(1, data.Count);
        var scale = new float[data.Features];
        for (var d = 0; d < data.Features; d++)
        {
            mean[d] /= n;
            var variance = Math.Max(0, square[d] / n - mean[d] * mean[d]);
            scale[d] = (float)(1 / Math.Sqrt(variance + 1e-6));
        }
        return ([.. mean.Select(m => (float)m)], scale);
    }

    private static void Optimise(SoftmaxModel model, SoftmaxModel? anchor, LabeledSet data, TrainingOptions options, IProgress<double>? progress)
    {
        if (data.Count == 0) return;
        var classes = model.Classes;
        var features = model.Features;
        var classWeight = BalancedClassWeights(data, classes);
        var weights = model.Weights;
        var bias = model.Bias;
        var anchorWeights = anchor?.Weights.ToArray();
        var anchorBias = anchor?.Bias.ToArray();
        var m = new float[weights.Length + bias.Length];
        var v = new float[m.Length];
        var gradient = new float[m.Length];
        var normalised = new float[features];
        var probabilities = new float[classes];
        var order = Enumerable.Range(0, data.Count).ToArray();
        var random = new Random(options.Seed);
        var step = 0;
        var batches = (order.Length + options.BatchSize - 1) / options.BatchSize;
        var totalSteps = (double)options.Epochs * batches;
        for (var epoch = 0; epoch < options.Epochs; epoch++)
        {
            random.Shuffle(order);
            for (var start = 0; start < order.Length; start += options.BatchSize)
            {
                Array.Clear(gradient);
                var batchWeight = 0f;
                var end = Math.Min(order.Length, start + options.BatchSize);
                for (var o = start; o < end; o++)
                {
                    var i = order[o];
                    var row = data.Row(i);
                    var mean = model.Mean;
                    var scale = model.Scale;
                    for (var d = 0; d < features; d++)
                        normalised[d] = (row[d] - mean[d]) * scale[d];
                    model.Predict(row, probabilities);
                    var label = (int)data.Label(i);
                    var w = data.Weight(i) * classWeight[label];
                    batchWeight += w;
                    for (var c = 0; c < classes; c++)
                    {
                        var g = (probabilities[c] - (c == label ? 1 : 0)) * w;
                        if (g == 0) continue;
                        var offset = c * features;
                        for (var d = 0; d < features; d++)
                            gradient[offset + d] += g * normalised[d];
                        gradient[weights.Length + c] += g;
                    }
                }
                if (batchWeight <= 0) continue;
                step++;
                var correction1 = 1 - MathF.Pow(Beta1, step);
                var correction2 = 1 - MathF.Pow(Beta2, step);
                var rate = (float)(options.LearningRate * 0.5 * (1 + Math.Cos(Math.PI * Math.Min(1, step / totalSteps))));
                for (var p = 0; p < gradient.Length; p++)
                {
                    var isWeight = p < weights.Length;
                    var value = isWeight ? weights[p] : bias[p - weights.Length];
                    var g = gradient[p] / batchWeight;
                    m[p] = Beta1 * m[p] + (1 - Beta1) * g;
                    v[p] = Beta2 * v[p] + (1 - Beta2) * g * g;
                    var update = rate * (m[p] / correction1) / (MathF.Sqrt(v[p] / correction2) + Epsilon);
                    var decay = isWeight ? options.L2 * value : 0;
                    if (anchorWeights is not null && anchorBias is not null)
                        decay += options.AnchorL2 * (value - (isWeight ? anchorWeights[p] : anchorBias[p - weights.Length]));
                    update += rate * decay;
                    if (isWeight) weights[p] -= update;
                    else bias[p - weights.Length] -= update;
                }
            }
            progress?.Report((epoch + 1.0) / options.Epochs);
        }
    }

    private static float[] BalancedClassWeights(LabeledSet data, int classes)
    {
        var totals = new double[classes];
        for (var i = 0; i < data.Count; i++)
            totals[(int)data.Label(i)] += data.Weight(i);
        var present = totals.Count(t => t > 0);
        var all = totals.Sum();
        return [.. totals.Select(t => t > 0 ? (float)(all / (present * t)) : 0f)];
    }
}
