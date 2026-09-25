using NAudio.Wave;

namespace AudioPlayer.Core.Audio;

/// <summary>
/// Sample-accurate fader. FadeTo is called from the UI thread, Read from the audio thread.
/// With endWhenDone, Read returns 0 once the fade completes so a MixingSampleProvider drops it.
/// </summary>
public sealed class FaderSampleProvider(ISampleProvider source, float initialGain = 0f) : ISampleProvider
{
    private readonly Lock gate = new();
    private float gain = initialGain, from, to;
    private long fadePos, fadeLen; // in frames
    private FadeCurve curve;
    private bool endWhenDone;

    public WaveFormat WaveFormat => source.WaveFormat;

    public float Gain { get { lock (gate) return gain; } }

    public bool IsFading { get { lock (gate) return fadePos < fadeLen; } }

    public void FadeTo(float target, int durationMs, FadeCurve curve, bool endWhenDone = false)
    {
        lock (gate)
        {
            from = gain;
            to = target;
            this.curve = curve;
            this.endWhenDone = endWhenDone;
            fadePos = 0;
            fadeLen = Math.Max(1, (long)durationMs * WaveFormat.SampleRate / 1000);
        }
    }

    public int Read(Span<float> buffer)
    {
        lock (gate)
        {
            if (endWhenDone && fadePos >= fadeLen) return 0;
        }

        int read = source.Read(buffer);
        int ch = WaveFormat.Channels;
        lock (gate)
        {
            for (int i = 0; i < read; i += ch)
            {
                if (fadePos < fadeLen)
                {
                    fadePos++;
                    gain = from + (to - from) * FadeCurves.Shape(curve, (float)fadePos / fadeLen, to > from);
                }
                for (int c = 0; c < ch; c++) buffer[i + c] *= gain;
            }
        }
        return read;
    }
}
