using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioPlayer.Core.Audio;

/// <summary>One playback instance of a file on one bus.</summary>
public sealed class TrackVoice : ISampleProvider, IDisposable
{
    private readonly Lock gate = new(); // Read (audio thread) vs. Seek/SetRange (UI thread)
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

    /// <summary>End point, or end of file when none is set.</summary>
    public TimeSpan End => looper.End ?? reader.TotalTime;

    public TimeSpan Remaining => End > Position ? End - Position : TimeSpan.Zero;

    /// <summary>Once set and the fade-out has finished, outputs silence without advancing the file.</summary>
    public volatile bool Paused;

    private bool autoFading;

    /// <summary>
    /// Fade-out applied automatically so playback reaches silence exactly at the end point (0 = off).
    /// Setting it re-arms the fade. Ignored while looping.
    /// </summary>
    public int AutoFadeOutMs
    {
        get;
        set { lock (gate) { field = value; autoFading = false; } }
    }

    public FadeCurve AutoFadeCurve { get; set; } = FadeCurve.EqualPower;

    public void SetRange(TimeSpan start, TimeSpan? end)
    {
        lock (gate)
        {
            looper.Start = start;
            looper.End = end;
        }
    }

    // ponytail: hard jump, may click when seeking audible audio; add a short dip-fade if operators seek live
    public void Seek(TimeSpan position)
    {
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (position > reader.TotalTime) position = reader.TotalTime;
        lock (gate)
        {
            reader.CurrentTime = position;
            if (autoFading && Remaining.TotalMilliseconds > AutoFadeOutMs)
            {
                autoFading = false; // moved back out of the fade zone: bring the level back
                Fader.FadeTo(1f, 30, FadeCurve.Linear);
            }
        }
    }

    public int Read(Span<float> buffer)
    {
        if (Paused && !Fader.IsFading)
        {
            buffer.Clear();
            return buffer.Length;
        }
        lock (gate)
        {
            if (AutoFadeOutMs > 0 && !autoFading && !Loop && !Paused)
            {
                var left = Remaining.TotalMilliseconds;
                if (left <= AutoFadeOutMs)
                {
                    Fader.FadeTo(0f, (int)left, AutoFadeCurve);
                    autoFading = true;
                }
            }
            return volume.Read(buffer);
        }
    }

    public void Dispose() => reader.Dispose();
}
