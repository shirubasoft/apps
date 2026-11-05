namespace QuietNight.Core.Analysis;

/// <summary>Triangular HTK mel filters over the listening spectrum.</summary>
public sealed class MelBank
{
    public const double LowHertz = 60;
    public const double HighHertz = 8_000;

    private readonly int[] first;
    private readonly float[][] weights;

    public MelBank(int bands = ListeningFormat.MelBands)
    {
        Bands = bands;
        first = new int[bands];
        weights = new float[bands][];
        var low = ToMel(LowHertz);
        var high = ToMel(HighHertz);
        var edges = new double[bands + 2];
        for (var i = 0; i < edges.Length; i++)
            edges[i] = ToHertz(low + (high - low) * i / (bands + 1)) / ListeningFormat.BinHertz;
        for (var m = 0; m < bands; m++)
        {
            var (left, centre, right) = (edges[m], edges[m + 1], edges[m + 2]);
            var start = Math.Max(0, (int)Math.Ceiling(left));
            var end = Math.Min(ListeningFormat.Bins - 1, (int)Math.Floor(right));
            var row = new List<float>();
            for (var k = start; k <= end; k++)
            {
                var w = k <= centre ? (k - left) / (centre - left) : (right - k) / (right - centre);
                row.Add((float)Math.Max(0, w));
            }
            if (row.Sum() <= 0)
            {
                start = Math.Clamp((int)Math.Round(centre), 0, ListeningFormat.Bins - 1);
                row = [1f];
            }
            first[m] = start;
            weights[m] = [.. row];
        }
    }

    public int Bands { get; }

    public void Apply(ReadOnlySpan<float> power, Span<float> bandPower)
    {
        for (var m = 0; m < Bands; m++)
        {
            var sum = 0f;
            var row = weights[m];
            for (var i = 0; i < row.Length; i++)
                sum += row[i] * power[first[m] + i];
            bandPower[m] = sum;
        }
    }

    private static double ToMel(double hertz) => 2595 * Math.Log10(1 + hertz / 700);

    private static double ToHertz(double mel) => 700 * (Math.Pow(10, mel / 2595) - 1);
}
