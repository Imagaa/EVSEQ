using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioPlayer.Core.Audio;

/// <summary>One playback instance of a file on one bus.</summary>
public sealed class TrackVoice : ISampleProvider, IDisposable
{
    private readonly AudioFileReader reader;
    private readonly LoopingReader looper;
    private readonly SmoothGainSampleProvider volume;

    public TrackVoice(string path, WaveFormat busFormat, float volume = 1f)
    {
        reader = new AudioFileReader(path);
        looper = new LoopingReader(reader);

        ISampleProvider chain = looper;
        if (chain.WaveFormat.Channels == 1 && busFormat.Channels == 2)
            chain = new MonoToStereoSampleProvider(chain);
        else if (chain.WaveFormat.Channels != busFormat.Channels)
        {
            reader.Dispose();
            throw new NotSupportedException($"{chain.WaveFormat.Channels}-channel audio is not supported.");
        }
        if (chain.WaveFormat.SampleRate != busFormat.SampleRate)
            chain = new WdlResamplingSampleProvider(chain, busFormat.SampleRate);

        Fader = new FaderSampleProvider(chain);
        this.volume = new SmoothGainSampleProvider(Fader, volume);
    }

    public FaderSampleProvider Fader { get; }

    public WaveFormat WaveFormat => volume.WaveFormat;

    public bool Loop { get => looper.Loop; set => looper.Loop = value; }

    public float Volume { get => volume.Gain; set => volume.Gain = value; }

    public TimeSpan Position => reader.CurrentTime;

    public TimeSpan Duration => reader.TotalTime;

    public TimeSpan Remaining => Duration - Position;

    /// <summary>Once set and the fade-out has finished, outputs silence without advancing the file.</summary>
    public volatile bool Paused;

    public int Read(Span<float> buffer)
    {
        if (Paused && !Fader.IsFading)
        {
            buffer.Clear();
            return buffer.Length;
        }
        return volume.Read(buffer);
    }

    public void Dispose() => reader.Dispose();
}
