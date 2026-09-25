using AudioPlayer.Core.Audio;

namespace AudioPlayer.Core.Tests;

public class OutputBusTests
{
    [Fact]
    public void EmptyBusOutputsSilenceForever()
    {
        using var bus = new OutputBus();
        var b = TestAudio.Read(bus.Output, 960);
        Assert.Equal(960, b.Length);
        Assert.All(b, s => Assert.Equal(0f, s));
    }

    [Fact]
    public void MixesInputsAtUnityMaster()
    {
        using var bus = new OutputBus();
        bus.Mixer.AddMixerInput(new ConstantSource(48000, 2));
        Assert.All(TestAudio.Read(bus.Output, 960), s => Assert.Equal(1f, s, 5));
    }

    [Fact]
    public void MasterVolumeRampsWithoutJump()
    {
        using var bus = new OutputBus();
        bus.Mixer.AddMixerInput(new ConstantSource(48000, 2));
        bus.VolumeDb = Db.Floor;
        var b = TestAudio.Read(bus.Output, 960);
        Assert.True(b[0] > 0.99f);   // starts near previous gain
        Assert.Equal(0f, b[^1], 4); // ends at target
    }
}
