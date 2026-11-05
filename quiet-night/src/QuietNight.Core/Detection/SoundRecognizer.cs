using QuietNight.Core.Training;

namespace QuietNight.Core.Detection;

/// <summary>
/// The slow, accurate stage: YAMNet features of a window, next to how they differ from the quiet room, classified by a
/// head trained on reference and user recordings. Comparing with the room keeps a fan or distant traffic hum, which
/// YAMNet alone may call a vehicle, from being mistaken for a car passing.
/// </summary>
public sealed class SoundRecognizer(SoundModel model, SoftmaxModel head, ListenerSettings settings)
{
    /// <summary>The window's features followed by their difference from the room's.</summary>
    public const int FeatureCount = 2 * SoundModel.FeatureCount;

    private const float RoomUpdateShare = 0.3f;

    private readonly float[] raw = new float[SoundModel.FeatureCount];
    private readonly float[] room = new float[SoundModel.FeatureCount];
    private readonly float[] features = new float[FeatureCount];
    private readonly float[] probabilities = new float[NoiseClasses.Count];

    public SoftmaxModel Head { get; } = head;

    public bool KnowsRoom { get; private set; }

    /// <summary>Learns what the room sounds like from a quiet window, averaging with earlier ones.</summary>
    public void LearnRoom(ReadOnlySpan<float> window)
    {
        model.Features(window, raw);
        for (var d = 0; d < raw.Length; d++)
            room[d] = KnowsRoom ? room[d] + RoomUpdateShare * (raw[d] - room[d]) : raw[d];
        KnowsRoom = true;
    }

    public SoundVerdict Classify(ReadOnlySpan<float> window)
    {
        model.Features(window, raw);
        Combine(raw, KnowsRoom ? room : raw, features);
        Head.Predict(features, probabilities);
        return settings.Judge(probabilities);
    }

    public static void Combine(ReadOnlySpan<float> window, ReadOnlySpan<float> room, Span<float> features)
    {
        window.CopyTo(features);
        for (var d = 0; d < window.Length; d++)
            features[SoundModel.FeatureCount + d] = window[d] - room[d];
    }
}
