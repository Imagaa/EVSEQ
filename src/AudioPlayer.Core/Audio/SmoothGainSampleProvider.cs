using NAudio.Wave;

namespace AudioPlayer.Core.Audio;

/// <summary>Real-time gain (track fader, master fader). Changes ramp across one buffer to avoid clicks.</summary>
public sealed class SmoothGainSampleProvider(ISampleProvider source, float gain = 1f) : ISampleProvider
{
    private float current = gain;

    public float Gain { get; set; } = gain;

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);
        int ch = WaveFormat.Channels;
        int frames = read / ch;
        if (frames == 0) return read;

        float target = Gain;
        float step = (target - current) / frames;
        for (int f = 0; f < frames; f++)
        {
            current += step;
            for (int c = 0; c < ch; c++) buffer[f * ch + c] *= current;
        }
        current = target;
        return read;
    }
}
