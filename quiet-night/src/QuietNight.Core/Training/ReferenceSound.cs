using System.Text.Json;
using System.Text.Json.Serialization;
using QuietNight.Core.Detection;

namespace QuietNight.Core.Training;

/// <summary>
/// One Freesound clip of the reference set, selected from FSD50K with a CC0 or CC-BY licence.
/// <see cref="Group"/> is <c>cat</c>, <c>motorcycle</c>, <c>car</c>, <c>other</c> (sounds that must not trigger
/// masking) or <c>background</c> (night-time rooms), and <see cref="Split"/> keeps test clips away from training.
/// </summary>
public sealed record ReferenceSound(
    int Id,
    string Group,
    string Split,
    string License,
    string Uploader,
    string Title,
    string Page,
    string Preview,
    string Sha256,
    IReadOnlyList<string> Labels)
{
    public bool IsBackground => Group == "background";

    public bool IsTest => Split == "test";

    public bool IsValidation => Split == "validation";

    public bool IsTraining => Split == "train";

    public NoiseClass Noise => Group switch
    {
        "cat" => NoiseClass.Cat,
        "motorcycle" => NoiseClass.Motorcycle,
        "car" => NoiseClass.Car,
        _ => NoiseClass.Other,
    };

    public static IReadOnlyList<ReferenceSound> ReadManifest(Stream stream) =>
        JsonSerializer.Deserialize(stream, ReferenceJson.Default.IReadOnlyListReferenceSound) ?? [];

    public static void WriteManifest(Stream stream, IReadOnlyList<ReferenceSound> sounds) =>
        JsonSerializer.Serialize(stream, sounds, ReferenceJson.Default.IReadOnlyListReferenceSound);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(IReadOnlyList<ReferenceSound>))]
internal sealed partial class ReferenceJson : JsonSerializerContext;
