using NAudio.Wave;

namespace AudioPlayer.Core.Audio;

public sealed class LoopingReader(AudioFileReader reader) : ISampleProvider
{
    public bool Loop { get; set; }

    public WaveFormat WaveFormat => reader.WaveFormat;

    public int Read(Span<float> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = reader.Read(buffer[total..]);
            if (n == 0)
            {
                if (!Loop || reader.Length == 0) break;
                reader.Position = 0;
                continue;
            }
            total += n;
        }
        return total;
    }
}
