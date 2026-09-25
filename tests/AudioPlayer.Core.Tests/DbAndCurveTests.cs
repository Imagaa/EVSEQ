using AudioPlayer.Core.Audio;

namespace AudioPlayer.Core.Tests;

public class DbAndCurveTests
{
    [Fact] public void ZeroDbIsUnityGain() => Assert.Equal(1f, Db.ToGain(0));
    [Fact] public void Minus6DbIsAboutHalf() => Assert.Equal(0.501f, Db.ToGain(-6), 3);
    [Fact] public void FloorIsSilence() => Assert.Equal(0f, Db.ToGain(Db.Floor));
    [Fact] public void BelowFloorIsSilence() => Assert.Equal(0f, Db.ToGain(-90));
    [Fact] public void UnityGainIsZeroDb() => Assert.Equal(0, Db.FromGain(1f), 6);
    [Fact] public void ZeroGainIsMinusInfinity() => Assert.Equal(double.NegativeInfinity, Db.FromGain(0f));

    [Fact] public void LinearMidpoint() => Assert.Equal(0.5f, FadeCurves.Shape(FadeCurve.Linear, 0.5f, rising: true), 4);
    [Fact] public void EqualPowerRisingMidpoint() => Assert.Equal(0.7071f, FadeCurves.Shape(FadeCurve.EqualPower, 0.5f, rising: true), 4);
    // falling: gain = from + (to-from)*shape = 1 - 0.2929 = 0.7071 at the midpoint
    [Fact] public void EqualPowerFallingMidpoint() => Assert.Equal(0.2929f, FadeCurves.Shape(FadeCurve.EqualPower, 0.5f, rising: false), 4);

    [Theory, InlineData(FadeCurve.Linear), InlineData(FadeCurve.EqualPower)]
    public void CurveEndpoints(FadeCurve c)
    {
        Assert.Equal(0f, FadeCurves.Shape(c, 0f, true), 5);
        Assert.Equal(1f, FadeCurves.Shape(c, 1f, true), 5);
        Assert.Equal(0f, FadeCurves.Shape(c, 0f, false), 5);
        Assert.Equal(1f, FadeCurves.Shape(c, 1f, false), 5);
    }
}
