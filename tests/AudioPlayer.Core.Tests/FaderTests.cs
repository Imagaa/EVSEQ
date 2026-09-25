using AudioPlayer.Core.Audio;

namespace AudioPlayer.Core.Tests;

public class FaderTests
{
    [Fact]
    public void StartsSilentByDefault()
    {
        var f = new FaderSampleProvider(new ConstantSource(1000, 1));
        Assert.All(TestAudio.Read(f, 10), s => Assert.Equal(0f, s));
    }

    [Fact]
    public void LinearFadeInReachesTargetOverDuration()
    {
        var f = new FaderSampleProvider(new ConstantSource(1000, 1));
        f.FadeTo(1f, 1000, FadeCurve.Linear);
        var b = TestAudio.Read(f, 1000);
        Assert.Equal(0.5f, b[499], 3);
        Assert.Equal(1f, b[999], 5);
        Assert.False(f.IsFading);
    }

    [Fact]
    public void EqualPowerFadeInMidpoint()
    {
        var f = new FaderSampleProvider(new ConstantSource(1000, 1));
        f.FadeTo(1f, 1000, FadeCurve.EqualPower);
        Assert.Equal(0.7071f, TestAudio.Read(f, 1000)[499], 3);
    }

    [Fact]
    public void StereoFramesShareOneGain()
    {
        var f = new FaderSampleProvider(new ConstantSource(1000, 2));
        f.FadeTo(1f, 1000, FadeCurve.Linear);
        var b = TestAudio.Read(f, 2000);
        Assert.Equal(b[998], b[999]);
    }

    [Fact]
    public void RetargetMidFadeContinuesFromCurrentGain()
    {
        var f = new FaderSampleProvider(new ConstantSource(1000, 1));
        f.FadeTo(1f, 100, FadeCurve.Linear);
        Assert.Equal(0.5f, TestAudio.Read(f, 50)[49], 3);
        f.FadeTo(0f, 100, FadeCurve.Linear);
        var next = TestAudio.Read(f, 1)[0];
        Assert.InRange(next, 0.49f, 0.5f); // no jump
    }

    [Fact]
    public void EndWhenDoneEndsStreamAfterFadeOut()
    {
        var f = new FaderSampleProvider(new ConstantSource(1000, 1), initialGain: 1f);
        f.FadeTo(0f, 10, FadeCurve.Linear, endWhenDone: true);
        var b = TestAudio.Read(f, 100);
        Assert.Equal(100, b.Length);
        Assert.Equal(0f, b[99]);
        Assert.Empty(TestAudio.Read(f, 100));
    }

    [Fact]
    public void SmoothGainRampsAcrossOneBuffer()
    {
        var g = new SmoothGainSampleProvider(new ConstantSource(1000, 1)) { Gain = 0f };
        var b = TestAudio.Read(g, 10);
        Assert.Equal(0.9f, b[0], 4);
        Assert.Equal(0f, b[9], 4);
        Assert.All(TestAudio.Read(g, 10), s => Assert.Equal(0f, s));
    }

    [Fact]
    public void SmoothGainInitialGainAppliesImmediately()
    {
        var g = new SmoothGainSampleProvider(new ConstantSource(1000, 1), 0.5f);
        Assert.All(TestAudio.Read(g, 10), s => Assert.Equal(0.5f, s));
    }
}
