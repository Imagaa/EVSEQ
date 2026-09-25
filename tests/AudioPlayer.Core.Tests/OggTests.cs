using AudioPlayer.Core.Audio;
using NAudio.Wave;

namespace AudioPlayer.Core.Tests;

/// <summary>OGG Vorbis (F15). Fixture: 1 s, 440 Hz at -6 dB, 44.1 kHz stereo, made with ffmpeg/libvorbis.</summary>
public class OggTests
{
    private static readonly string Ogg = Path.Combine(AppContext.BaseDirectory, "TestData", "tone-1s.ogg");

    [Fact]
    public void ReadsDuration() => Assert.InRange(AudioInfo.GetDuration(Ogg).TotalMilliseconds, 990, 1010);

    [Fact]
    public void PlaysThroughTrackVoiceAtBusRate()
    {
        using var v = new TrackVoice(Ogg, WaveFormat.CreateIeeeFloatWaveFormat(48000, 2));
        v.Fader.FadeTo(1f, 0, FadeCurve.Linear);
        int total = 0, n;
        var buffer = new float[9600];
        float peak = 0;
        while ((n = v.Read(buffer)) > 0)
        {
            total += n;
            for (int i = 0; i < n; i++) peak = Math.Max(peak, Math.Abs(buffer[i]));
        }
        Assert.InRange(total, 95000, 97000); // ~1 s of 48 kHz stereo after resampling
        Assert.InRange(peak, 0.4f, 0.6f);
    }

    [Fact]
    public void ComputesWaveform()
    {
        var peaks = Waveform.ComputePeaks(Ogg, buckets: 50);
        Assert.All(peaks[2..^2], p => Assert.InRange(p, 0.4f, 0.6f));
    }

    [Fact]
    public void FolderScanIncludesOgg()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ap-ogg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.Copy(Ogg, Path.Combine(dir, "a.OGG"));
        Assert.Single(AudioFiles.InFolder(dir));
    }
}
