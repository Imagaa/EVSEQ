using NAudio.Wave;

namespace AudioPlayer.Core.Audio;

/// <summary>Plays the file between Start and End (end of file when null), optionally looping back to Start.</summary>
public sealed class LoopingReader(AudioFileReader reader) : ISampleProvider
{
    public bool Loop { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan? End { get; set; }

    public WaveFormat WaveFormat => reader.WaveFormat;

    public int Read(Span<float> buffer)
    {
        int ch = WaveFormat.Channels;
        int total = 0;
        bool wrapped = false; // guards against spinning when the range is empty
        while (total < buffer.Length)
        {
            int want = buffer.Length - total;
            if (End is { } end)
            {
                long left = (long)((end - reader.CurrentTime).TotalSeconds * WaveFormat.SampleRate) * ch;
                want = (int)Math.Min(want, Math.Max(0, left));
            }

            int n = want > 0 ? reader.Read(buffer.Slice(total, want)) : 0;
            if (n == 0)
            {
                if (!Loop || reader.Length == 0 || wrapped) break;
                reader.CurrentTime = Start;
                wrapped = true;
                continue;
            }
            total += n;
            wrapped = false;
        }
        return total;
    }
}
