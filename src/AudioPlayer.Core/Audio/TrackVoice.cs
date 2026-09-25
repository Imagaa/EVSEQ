using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioPlayer.Core.Audio;

/// <summary>One playback instance of a file on one bus.</summary>
public sealed class TrackVoice : ISampleProvider, IDisposable
{
    private readonly Lock gate = new(); // Read (audio thread) vs. Seek/SetRange (UI thread)
    private readonly WaveStream reader;
    private readonly LoopingReader looper;
    private readonly SmoothGainSampleProvider volume;

    public TrackVoice(string path, WaveFormat busFormat, float volume = 1f)
    {
        reader = AudioFiles.OpenReader(path);
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

    /// <summary>Current position; a seek that is still waiting for the next buffer already counts.</summary>
    public TimeSpan Position
    {
        get { lock (gate) return PositionUnlocked; }
    }

    public TimeSpan Duration => reader.TotalTime;

    /// <summary>End point, or end of file when none is set.</summary>
    public TimeSpan End => looper.End ?? reader.TotalTime;

    public TimeSpan Remaining
    {
        get { lock (gate) return RemainingUnlocked; }
    }

    private const int DeclickFrames = 240; // 5 ms at 48 kHz on each side of a seek
    private TimeSpan? pendingSeek;         // applied inside the next Read, between a ramp down and a ramp up

    private TimeSpan PositionUnlocked => pendingSeek ?? reader.CurrentTime;
    private TimeSpan RemainingUnlocked => End > PositionUnlocked ? End - PositionUnlocked : TimeSpan.Zero;

    /// <summary>Once set and the fade-out has finished, outputs silence without advancing the file.</summary>
    public volatile bool Paused;

    private bool autoFading;

    /// <summary>
    /// Fade-out applied automatically so playback reaches silence exactly at its end (end point or end of file; 0 = off).
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

    /// <summary>
    /// Moves the play position. With <paramref name="smooth"/> (the default) the jump happens inside the next
    /// audio buffer between a 5 ms ramp down and a 5 ms ramp up, so seeking audible audio never clicks.
    /// Silent (paused) voices and the initial start position jump immediately.
    /// </summary>
    public void Seek(TimeSpan position, bool smooth = true)
    {
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (position > reader.TotalTime) position = reader.TotalTime;
        lock (gate)
        {
            if (smooth && !(Paused && !Fader.IsFading)) pendingSeek = position;
            else
            {
                pendingSeek = null;
                ApplySeek(position);
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
                var left = RemainingUnlocked.TotalMilliseconds;
                if (left <= AutoFadeOutMs)
                {
                    Fader.FadeTo(0f, (int)left, AutoFadeCurve);
                    autoFading = true;
                }
            }
            return pendingSeek is { } target ? ReadAcrossSeek(buffer, target) : volume.Read(buffer);
        }
    }

    /// <summary>Old position ramped down, jump, new position ramped up — all within this buffer.</summary>
    private int ReadAcrossSeek(Span<float> buffer, TimeSpan target)
    {
        int ch = WaveFormat.Channels;
        int rampFrames = Math.Max(1, Math.Min(DeclickFrames, buffer.Length / ch / 2));

        int before = volume.Read(buffer[..(rampFrames * ch)]);
        int framesBefore = before / ch;
        for (int f = 0; f < framesBefore; f++)
        {
            float g = 1f - (f + 1f) / framesBefore;
            for (int c = 0; c < ch; c++) buffer[f * ch + c] *= g;
        }

        pendingSeek = null;
        ApplySeek(target);

        int after = volume.Read(buffer[before..]);
        int framesUp = Math.Min(rampFrames, after / ch);
        for (int f = 0; f < framesUp; f++)
        {
            float g = (f + 1f) / (framesUp + 1f);
            for (int c = 0; c < ch; c++) buffer[before + f * ch + c] *= g;
        }
        return before + after;
    }

    private void ApplySeek(TimeSpan position)
    {
        reader.CurrentTime = position;
        if (autoFading && RemainingUnlocked.TotalMilliseconds > AutoFadeOutMs)
        {
            autoFading = false; // moved back out of the fade zone: bring the level back
            Fader.FadeTo(1f, 30, FadeCurve.Linear);
        }
    }

    public void Dispose() => reader.Dispose();
}
