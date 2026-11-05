using Concentus;
using Concentus.Oggfile;
using QuietNight.Core.Analysis;

namespace QuietNight.Tests.Support;

/// <summary>Decodes the committed Ogg Opus reference sounds straight to the listening rate.</summary>
public static class OpusAudio
{
    public static float[] Decode(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = OpusCodecFactory.CreateDecoder(ListeningFormat.SampleRate, 1);
        var ogg = new OpusOggReadStream(decoder, stream);
        var samples = new List<float>();
        while (ogg.HasNextPacket)
        {
            var packet = ogg.DecodeNextPacket();
            if (packet is null) continue;
            foreach (var value in packet)
                samples.Add(value / 32768f);
        }
        return [.. samples];
    }
}
