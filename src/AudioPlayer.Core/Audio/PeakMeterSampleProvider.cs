using NAudio.Wave;

namespace AudioPlayer.Core.Audio;

/// <summary>
/// Pass-through that remembers the highest absolute sample per channel (first two channels) since the
/// last <see cref="TakePeaks"/>. Read runs on the audio thread, TakePeaks on the UI thread.
/// </summary>
public sealed class PeakMeterSampleProvider(ISampleProvider source) : ISampleProvider
{
    private readonly Lock gate = new();
    private float left, right;

    public WaveFormat WaveFormat => source.WaveFormat;

    public int Read(Span<float> buffer)
    {
        int read = source.Read(buffer);
        int ch = WaveFormat.Channels;
        float l = 0, r = 0;
        for (int i = 0; i < read; i += ch)
        {
            l = Math.Max(l, Math.Abs(buffer[i]));
            r = Math.Max(r, Math.Abs(buffer[ch > 1 ? i + 1 : i]));
        }
        lock (gate)
        {
            left = Math.Max(left, l);
            right = Math.Max(right, r);
        }
        return read;
    }

    /// <summary>Linear peaks (1.0 = 0 dBFS) since the previous call; resets them.</summary>
    public (float Left, float Right) TakePeaks()
    {
        lock (gate)
        {
            var p = (left, right);
            left = right = 0;
            return p;
        }
    }
}
