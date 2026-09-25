using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioPlayer.Core.Audio;

/// <summary>One output path (Main or Monitor): mixer → master gain → WASAPI device.</summary>
public sealed class OutputBus : IDisposable
{
    public static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    private readonly SmoothGainSampleProvider master;
    private WasapiPlayer? player;

    public OutputBus()
    {
        Mixer = new MixingSampleProvider(Format) { ReadFully = true };
        master = new SmoothGainSampleProvider(Mixer);
    }

    public MixingSampleProvider Mixer { get; }

    public ISampleProvider Output => master;

    public double VolumeDb
    {
        get;
        set { field = value; master.Gain = Db.ToGain(value); }
    }

    public string? DeviceId { get; private set; }

    public event EventHandler<Exception>? PlaybackFailed;

    public void AttachDevice(MMDevice? device)
    {
        DetachDevice();
        if (device is null) return;

        var p = new WasapiPlayerBuilder().WithDevice(device).WithLatency(50).Build();
        p.Init(new SampleToWaveProvider(master));
        p.PlaybackStopped += (_, e) => { if (e.Exception is not null) PlaybackFailed?.Invoke(this, e.Exception); };
        p.Play();
        player = p;
        DeviceId = device.ID;
    }

    public void DetachDevice()
    {
        try
        {
            player?.Stop();
            player?.Dispose();
        }
        catch (COMException)
        {
            // device already gone (unplugged); nothing left to release on its side
        }
        finally
        {
            player = null;
            DeviceId = null;
        }
    }

    public void Dispose() => DetachDevice();
}
