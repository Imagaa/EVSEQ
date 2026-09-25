using AudioPlayer.Core.Audio;
using NAudio.Wave;

namespace AudioPlayer.Core.Tests;

public class TrackVoiceTests
{
    private static readonly WaveFormat Bus = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    private static TrackVoice Open(string path)
    {
        var v = new TrackVoice(path, Bus);
        v.Fader.FadeTo(1f, 0, FadeCurve.Linear);
        return v;
    }

    [Fact]
    public void ConvertsMono44kToBusFormat()
    {
        using var v = Open(TestAudio.CreateWav(44100, 1, 0.2));
        Assert.Equal(48000, v.WaveFormat.SampleRate);
        Assert.Equal(2, v.WaveFormat.Channels);
        Assert.NotEmpty(TestAudio.Read(v, 4800));
    }

    [Fact]
    public void StreamEndsWhenFileEnds()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 0.1)); // 4800 frames = 9600 samples
        Assert.Equal(9600, TestAudio.Read(v, 20000).Length);
        Assert.Empty(TestAudio.Read(v, 20000));
    }

    [Fact]
    public void LoopKeepsStreamAlive()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 0.1));
        v.Loop = true;
        Assert.Equal(96000, TestAudio.Read(v, 96000).Length);
    }

    [Fact]
    public void RemainingFollowsPosition()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 0.2));
        TestAudio.Read(v, 9600); // 0.1 s
        Assert.InRange(v.Remaining.TotalMilliseconds, 90, 110);
    }

    [Fact]
    public void AudioInfoReadsDuration()
    {
        var d = AudioInfo.GetDuration(TestAudio.CreateWav(48000, 2, 0.25));
        Assert.InRange(d.TotalMilliseconds, 240, 260);
    }
}
