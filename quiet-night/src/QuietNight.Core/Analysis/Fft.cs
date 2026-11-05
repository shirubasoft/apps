namespace QuietNight.Core.Analysis;

/// <summary>In-place radix-2 complex FFT for power-of-two sizes.</summary>
public sealed class Fft
{
    private readonly int[] reversed;
    private readonly float[] cos;
    private readonly float[] sin;

    public Fft(int size)
    {
        if (size < 2 || (size & (size - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "FFT size must be a power of two.");
        Size = size;
        reversed = new int[size];
        var bits = int.Log2(size);
        for (var i = 0; i < size; i++)
            reversed[i] = ReverseBits(i, bits);
        cos = new float[size / 2];
        sin = new float[size / 2];
        for (var i = 0; i < size / 2; i++)
        {
            var angle = -2 * Math.PI * i / size;
            cos[i] = (float)Math.Cos(angle);
            sin[i] = (float)Math.Sin(angle);
        }
    }

    public int Size { get; }

    public void Forward(Span<float> real, Span<float> imaginary) => Transform(real, imaginary, 1);

    public void Inverse(Span<float> real, Span<float> imaginary)
    {
        Transform(real, imaginary, -1);
        var scale = 1f / Size;
        for (var i = 0; i < Size; i++)
        {
            real[i] *= scale;
            imaginary[i] *= scale;
        }
    }

    private void Transform(Span<float> real, Span<float> imaginary, int direction)
    {
        if (real.Length != Size || imaginary.Length != Size)
            throw new ArgumentException($"Both buffers must hold {Size} values.");
        for (var i = 0; i < Size; i++)
        {
            var j = reversed[i];
            if (j <= i) continue;
            (real[i], real[j]) = (real[j], real[i]);
            (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
        }
        for (var length = 2; length <= Size; length <<= 1)
        {
            var half = length / 2;
            var step = Size / length;
            for (var start = 0; start < Size; start += length)
            {
                for (var k = 0; k < half; k++)
                {
                    var wr = cos[k * step];
                    var wi = direction * sin[k * step];
                    var a = start + k;
                    var b = a + half;
                    var tr = wr * real[b] - wi * imaginary[b];
                    var ti = wr * imaginary[b] + wi * real[b];
                    real[b] = real[a] - tr;
                    imaginary[b] = imaginary[a] - ti;
                    real[a] += tr;
                    imaginary[a] += ti;
                }
            }
        }
    }

    private static int ReverseBits(int value, int bits)
    {
        var result = 0;
        for (var i = 0; i < bits; i++)
        {
            result = (result << 1) | (value & 1);
            value >>= 1;
        }
        return result;
    }
}
