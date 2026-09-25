using AudioPlayer.Core.Audio;
using NAudio.Wave;

namespace AudioPlayer.Core.Tests;

public class WaveformTests
{
    /// <summary>1 s of a 0.5-amplitude tone followed by 1 s of silence.</summary>
    private static string ToneThenSilence()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ap-wave-{Guid.NewGuid():N}.wav");
        var samples = new float[48000 * 2];
        for (int i = 0; i < 48000; i++) samples[i] = 0.5f * MathF.Sin(2 * MathF.PI * 440 * i / 48000f);
        using (var w = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1)))
            w.WriteSamples(samples, 0, samples.Length);
        return path;
    }

    [Fact]
    public void PeaksFollowTheSignal()
    {
        var peaks = Waveform.ComputePeaks(ToneThenSilence(), buckets: 100);

        Assert.Equal(100, peaks.Length);
        Assert.All(peaks[..48], p => Assert.InRange(p, 0.45f, 0.51f)); // tone half
        Assert.All(peaks[52..], p => Assert.Equal(0f, p));              // silent half
    }

    [Fact]
    public void CancellationStopsDecoding()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => Waveform.ComputePeaks(ToneThenSilence(), ct: cts.Token));
    }
}
