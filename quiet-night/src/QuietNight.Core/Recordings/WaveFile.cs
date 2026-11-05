using System.Buffers.Binary;
using System.Text;

namespace QuietNight.Core.Recordings;

/// <summary>Reads and writes 16-bit PCM WAV files. Multi-channel input is mixed down to mono.</summary>
public static class WaveFile
{
    public static void Write(Stream stream, ReadOnlySpan<float> samples, int sampleRate)
    {
        var dataBytes = samples.Length * 2;
        Span<byte> header = stackalloc byte[44];
        Encoding.ASCII.GetBytes("RIFF", header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], 36 + dataBytes);
        Encoding.ASCII.GetBytes("WAVEfmt ", header[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[22..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..], sampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[34..], 16);
        Encoding.ASCII.GetBytes("data", header[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..], dataBytes);
        stream.Write(header);
        var buffer = new byte[Math.Min(dataBytes, 64 * 1024)];
        for (var start = 0; start < samples.Length; start += buffer.Length / 2)
        {
            var count = Math.Min(buffer.Length / 2, samples.Length - start);
            for (var i = 0; i < count; i++)
            {
                var value = (short)Math.Clamp(MathF.Round(samples[start + i] * 32767), short.MinValue, short.MaxValue);
                BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(i * 2), value);
            }
            stream.Write(buffer, 0, count * 2);
        }
    }

    public static (float[] Samples, int SampleRate) Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
            throw new InvalidDataException("Not a RIFF file.");
        reader.ReadInt32();
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
            throw new InvalidDataException("Not a WAVE file.");
        int channels = 0, sampleRate = 0, bits = 0;
        while (stream.Position < stream.Length)
        {
            var id = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var size = reader.ReadInt32();
            if (id == "fmt ")
            {
                var format = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadInt16();
                bits = reader.ReadInt16();
                if (format != 1 || bits != 16)
                    throw new InvalidDataException("Only 16-bit PCM WAV files are supported.");
                reader.ReadBytes(size - 16);
            }
            else if (id == "data")
            {
                if (channels == 0) throw new InvalidDataException("The WAV file has no format chunk.");
                var bytes = reader.ReadBytes(size);
                var frames = bytes.Length / (2 * channels);
                var samples = new float[frames];
                for (var f = 0; f < frames; f++)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++)
                        sum += BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan((f * channels + c) * 2)) / 32768f;
                    samples[f] = sum / channels;
                }
                return (samples, sampleRate);
            }
            else
            {
                reader.ReadBytes(size + (size & 1));
            }
        }
        throw new InvalidDataException("The WAV file has no data chunk.");
    }
}
