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
    public void RangePlaysOnlyBetweenStartAndEnd()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 0.2));
        v.SetRange(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));
        v.Seek(TimeSpan.FromMilliseconds(50));
        Assert.Equal(4800, TestAudio.Read(v, 20000).Length); // 50 ms of stereo
        Assert.InRange(v.Position.TotalMilliseconds, 99, 101);
    }

    [Fact]
    public void LoopWrapsToStartPoint()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 0.2));
        v.SetRange(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));
        v.Seek(TimeSpan.FromMilliseconds(50));
        v.Loop = true;
        Assert.Equal(96000, TestAudio.Read(v, 96000).Length);
        Assert.InRange(v.Position.TotalMilliseconds, 50, 100);
    }

    [Fact]
    public void LoopWithEmptyRangeDoesNotHang()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 0.2));
        v.SetRange(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        v.Seek(TimeSpan.FromMilliseconds(100));
        v.Loop = true;
        Assert.Empty(TestAudio.Read(v, 960));
    }

    [Fact]
    public void SeekMovesPositionAndRemaining()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 0.2));
        v.Seek(TimeSpan.FromMilliseconds(150));
        Assert.InRange(v.Remaining.TotalMilliseconds, 49, 51);
        v.SetRange(TimeSpan.Zero, TimeSpan.FromMilliseconds(180));
        Assert.InRange(v.Remaining.TotalMilliseconds, 29, 31); // remaining counts to the end point
    }

    private static float Rms(float[] b) => MathF.Sqrt(b.Select(x => x * x).Average());

    [Fact]
    public void AutoFadeOutReachesSilenceAtEndPoint()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 1));
        v.SetRange(TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        v.AutoFadeCurve = FadeCurve.Linear;
        v.AutoFadeOutMs = 200;

        TestAudio.Read(v, 28800);                  // 0–300 ms: before the fade zone
        var tail = TestAudio.Read(v, 19200);       // 300–500 ms: the fade
        var start = Rms(tail[..960]);
        var end = Rms(tail[^960..]);
        Assert.True(start > 0.3f, $"fade should start near full level, was {start}");
        Assert.True(end < 0.05f * start, $"fade should end near silence, was {end}");
    }

    [Fact]
    public void AutoFadeOutIsSkippedWhileLooping()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 1));
        v.SetRange(TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        v.AutoFadeOutMs = 200;
        v.Loop = true;
        TestAudio.Read(v, 28800);
        Assert.True(Rms(TestAudio.Read(v, 19200)[^960..]) > 0.3f);
    }

    [Fact]
    public void SeekingBackOutOfFadeZoneRestoresLevel()
    {
        using var v = Open(TestAudio.CreateWav(48000, 2, 1));
        v.SetRange(TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        v.AutoFadeCurve = FadeCurve.Linear;
        v.AutoFadeOutMs = 200;
        v.Seek(TimeSpan.FromMilliseconds(450));
        TestAudio.Read(v, 960);                    // inside the fade zone: fade starts
        v.Seek(TimeSpan.FromMilliseconds(100));
        TestAudio.Read(v, 9600);                   // restore ramp
        Assert.True(Rms(TestAudio.Read(v, 960)) > 0.3f);
    }

    [Fact]
    public void AudioInfoReadsDuration()
    {
        var d = AudioInfo.GetDuration(TestAudio.CreateWav(48000, 2, 0.25));
        Assert.InRange(d.TotalMilliseconds, 240, 260);
    }
}
