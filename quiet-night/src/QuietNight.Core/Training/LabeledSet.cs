using QuietNight.Core.Detection;

namespace QuietNight.Core.Training;

/// <summary>Feature vectors with their classes and weights, growable and storable at half precision.</summary>
public sealed class LabeledSet(int features)
{
    private const uint Magic = 0x5358_4E51; // "QNXS"
    private const int Version = 1;

    private readonly List<float> values = [];
    private readonly List<NoiseClass> labels = [];
    private readonly List<float> weights = [];

    public int Features { get; } = features;

    public int Count => labels.Count;

    public ReadOnlySpan<float> Row(int index) =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(values).Slice(index * Features, Features);

    public NoiseClass Label(int index) => labels[index];

    public float Weight(int index) => weights[index];

    public void Add(ReadOnlySpan<float> row, NoiseClass label, float weight = 1)
    {
        if (row.Length != Features)
            throw new ArgumentException($"Expected {Features} features but got {row.Length}.", nameof(row));
        foreach (var value in row)
            values.Add(value);
        labels.Add(label);
        weights.Add(weight);
    }

    public void AddRange(LabeledSet other, float weightScale = 1)
    {
        for (var i = 0; i < other.Count; i++)
            Add(other.Row(i), other.Label(i), other.Weight(i) * weightScale);
    }

    public int CountOf(NoiseClass label) => labels.Count(l => l == label);

    /// <summary>A deterministic subset with at most <paramref name="perClass"/> rows of each class.</summary>
    public LabeledSet Sample(int perClass, int seed)
    {
        var random = new Random(seed);
        var result = new LabeledSet(Features);
        foreach (var group in Enumerable.Range(0, Count).GroupBy(Label).OrderBy(g => g.Key))
        {
            var rows = group.ToArray();
            random.Shuffle(rows);
            foreach (var row in rows.Take(perClass).Order())
                result.Add(Row(row), Label(row), Weight(row));
        }
        return result;
    }

    /// <summary>Splits rows into training and validation parts, keeping every class in both when it has two rows or more.</summary>
    public (LabeledSet Training, LabeledSet Validation) Split(double validationShare, int seed)
    {
        var random = new Random(seed);
        var training = new LabeledSet(Features);
        var validation = new LabeledSet(Features);
        foreach (var group in Enumerable.Range(0, Count).GroupBy(Label))
        {
            var rows = group.ToArray();
            random.Shuffle(rows);
            var held = rows.Length < 2 ? 0 : Math.Max(1, (int)Math.Round(rows.Length * validationShare));
            for (var i = 0; i < rows.Length; i++)
                (i < held ? validation : training).Add(Row(rows[i]), Label(rows[i]), Weight(rows[i]));
        }
        return (training, validation);
    }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(Features);
        writer.Write(Count);
        for (var i = 0; i < Count; i++)
        {
            writer.Write((byte)labels[i]);
            writer.Write((Half)weights[i]);
            foreach (var value in Row(i))
                writer.Write((Half)value);
        }
    }

    public static LabeledSet Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic || reader.ReadInt32() != Version)
            throw new InvalidDataException("This is not a Quiet Night example file.");
        var set = new LabeledSet(reader.ReadInt32());
        var count = reader.ReadInt32();
        var row = new float[set.Features];
        for (var i = 0; i < count; i++)
        {
            var label = (NoiseClass)reader.ReadByte();
            var weight = (float)reader.ReadHalf();
            for (var d = 0; d < row.Length; d++)
                row[d] = (float)reader.ReadHalf();
            set.Add(row, label, weight);
        }
        return set;
    }
}
