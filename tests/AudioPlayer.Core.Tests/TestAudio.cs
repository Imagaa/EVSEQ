using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioPlayer.Core.Tests;

/// <summary>Emits 1.0 on every channel for totalFrames frames, then ends.</summary>
sealed class ConstantSource(int sampleRate, int channels, long totalFrames = long.MaxValue) : ISampleProvider
{
    private long framesLeft = totalFrames;

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);

    public int Read(Span<float> buffer)
    {
        int frames = (int)Math.Min(buffer.Length / WaveFormat.Channels, framesLeft);
        int n = frames * WaveFormat.Channels;
        buffer[..n].Fill(1f);
        framesLeft -= frames;
        return n;
    }
}

static class TestAudio
{
    public static string CreateWav(int sampleRate, int channels, double seconds)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ap-test-{Guid.NewGuid():N}.wav");
        var gen = new SignalGenerator(sampleRate, channels) { Frequency = 440, Gain = 0.5 };
        WaveFileWriter.CreateWaveFile16(path, gen.Take(TimeSpan.FromSeconds(seconds)));
        return path;
    }

    public static float[] Read(ISampleProvider p, int samples)
    {
        var b = new float[samples];
        int n = p.Read(b);
        Array.Resize(ref b, n);
        return b;
    }
}
