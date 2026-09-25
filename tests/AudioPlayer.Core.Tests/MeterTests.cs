using AudioPlayer.Core.Audio;
using NAudio.Wave;

namespace AudioPlayer.Core.Tests;

public class MeterTests
{
    /// <summary>Stereo source: left = 0.5, right = 0.25.</summary>
    private sealed class LeftRightSource : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        public int Read(Span<float> buffer)
        {
            for (int i = 0; i < buffer.Length; i += 2) { buffer[i] = 0.5f; buffer[i + 1] = -0.25f; }
            return buffer.Length;
        }
    }

    [Fact]
    public void PeakMeterReportsPerChannelPeaksAndResets()
    {
        var m = new PeakMeterSampleProvider(new LeftRightSource());
        TestAudio.Read(m, 960);
        Assert.Equal((0.5f, 0.25f), m.TakePeaks());
        Assert.Equal((0f, 0f), m.TakePeaks()); // nothing played since
    }

    [Fact]
    public void PeakMeterKeepsHighestAcrossReads()
    {
        var m = new PeakMeterSampleProvider(new LeftRightSource());
        TestAudio.Read(m, 960);
        TestAudio.Read(m, 960);
        Assert.Equal(0.5f, m.TakePeaks().Left);
    }

    [Fact]
    public void OutputBusMetersAfterMasterGain()
    {
        using var bus = new OutputBus();
        bus.Mixer.AddMixerInput(new LeftRightSource());
        bus.VolumeDb = -6;
        TestAudio.Read(bus.Output, 960); // gain ramps to -6 dB over this buffer
        bus.TakePeaks();
        TestAudio.Read(bus.Output, 960);
        Assert.Equal(0.5f * Db.ToGain(-6), bus.TakePeaks().Left, 3);
    }

    [Fact]
    public void BallisticsAttackInstantlyAndFallSteadily()
    {
        var b = new MeterBallistics();
        b.Update(1f, 0.033);
        Assert.Equal(0, b.LevelDb, 3);
        b.Update(0f, 0.5);
        Assert.Equal(-12, b.LevelDb, 3); // 24 dB/s × 0.5 s
    }

    [Fact]
    public void HoldStaysThenFalls()
    {
        var b = new MeterBallistics();
        b.Update(0.5f, 0.033);                        // ≈ -6 dB
        double held = b.HoldDb;
        b.Update(0f, 1.0);
        Assert.Equal(held, b.HoldDb, 3);              // still within the 1.5 s hold
        b.Update(0f, 1.0);                            // past the hold time
        Assert.True(b.HoldDb < held);
    }

    [Fact]
    public void ClipLatchesUntilReset()
    {
        var b = new MeterBallistics();
        b.Update(1f, 0.033);
        b.Update(0f, 5);
        Assert.True(b.Clipped);
        b.ResetClip();
        Assert.False(b.Clipped);
    }

    [Fact]
    public void BelowFullScaleIsNotClipping()
    {
        var b = new MeterBallistics();
        b.Update(0.99f, 0.033);
        Assert.False(b.Clipped);
    }
}
