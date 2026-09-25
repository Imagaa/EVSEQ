using System.Collections.Concurrent;
using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Model;
using NAudio.Wave;

namespace AudioPlayer.Core.Engine;

/// <summary>
/// All transport logic. Every public member must be called on the UI thread;
/// the audio threads only read TrackVoice samples.
/// </summary>
public sealed class PlayerEngine : IDisposable
{
    private sealed class Voice(Track track, BusKind bus, TrackVoice audio)
    {
        public Track Track { get; } = track;
        public BusKind Bus { get; } = bus;
        public TrackVoice Audio { get; } = audio;
        public PlayState State { get; set; }
    }

    private readonly List<Voice> active = [];   // Playing or Paused
    private readonly List<Voice> draining = []; // stopped, still fading out
    private readonly ConcurrentQueue<ISampleProvider> ended = new();

    public PlayerEngine(OutputBus main, OutputBus monitor)
    {
        Main = main;
        Monitor = monitor;
        main.Mixer.MixerInputEnded += (_, e) => ended.Enqueue(e.SampleProvider);
        monitor.Mixer.MixerInputEnded += (_, e) => ended.Enqueue(e.SampleProvider);
    }

    public OutputBus Main { get; }
    public OutputBus Monitor { get; }
    public FadeSettings DefaultFade { get; set; } = new();

    /// <summary>A track reached the end of its file (not raised for Stop or Panic).</summary>
    public event Action<Track, BusKind>? TrackEnded;

    public PlayState GetState(Track t, BusKind bus) => Find(t, bus)?.State ?? PlayState.Stopped;
    public TimeSpan? GetPosition(Track t, BusKind bus) => Find(t, bus)?.Audio.Position;
    public TimeSpan? GetRemaining(Track t, BusKind bus) => Find(t, bus)?.Audio.Remaining;

    public void Play(Track t)
    {
        var v = Start(t, BusKind.Main); // throws before anything else is touched
        if (t.Overlay) return;
        foreach (var other in active.Where(o => o.Bus == BusKind.Main && o != v && !o.Track.Overlay).ToList())
            StopVoice(other);
    }

    public void Preview(Track t)
    {
        if (Find(t, BusKind.Monitor) is { State: PlayState.Playing } playing)
        {
            StopVoice(playing);
            return;
        }
        var v = Start(t, BusKind.Monitor);
        foreach (var other in active.Where(o => o.Bus == BusKind.Monitor && o != v).ToList())
            StopVoice(other);
    }

    public void Pause(Track t, BusKind bus)
    {
        if (Find(t, bus) is not { State: PlayState.Playing } v) return;
        var f = FadeOf(t);
        v.State = PlayState.Paused;
        v.Audio.Fader.FadeTo(0f, f.FadeOutMs, f.Curve);
        v.Audio.Paused = true;
    }

    public void Stop(Track t, BusKind bus)
    {
        if (Find(t, bus) is { } v) StopVoice(v);
    }

    public void Seek(Track t, BusKind bus, TimeSpan position) => Find(t, bus)?.Audio.Seek(position);

    public void Refresh(Track t)
    {
        foreach (var v in active.Where(v => v.Track == t))
        {
            v.Audio.Volume = Db.ToGain(t.VolumeDb);
            v.Audio.Loop = t.Loop;
            v.Audio.SetRange(StartOf(t), EndOf(t));
            ArmAutoFade(v);
        }
    }

    public void Panic()
    {
        // RemoveAllMixerInputs takes the mixer lock, so no Read is using these voices once it returns.
        Main.Mixer.RemoveAllMixerInputs();
        Monitor.Mixer.RemoveAllMixerInputs();
        foreach (var v in active.Concat(draining)) v.Audio.Dispose();
        active.Clear();
        draining.Clear();
        ended.Clear();
    }

    public void Pump()
    {
        while (ended.TryDequeue(out var provider))
        {
            if (draining.Find(v => v.Audio == provider) is { } d)
            {
                draining.Remove(d);
                d.Audio.Dispose();
            }
            else if (active.Find(v => v.Audio == provider) is { } a)
            {
                active.Remove(a);
                a.Audio.Dispose();
                TrackEnded?.Invoke(a.Track, a.Bus);
            }
        }
    }

    public void Dispose() => Panic();

    private Voice Start(Track t, BusKind bus)
    {
        var v = Find(t, bus);
        if (v is { State: PlayState.Playing }) return v;

        if (v is null)
        {
            v = new Voice(t, bus, new TrackVoice(t.FilePath, OutputBus.Format, Db.ToGain(t.VolumeDb)));
            v.Audio.SetRange(StartOf(t), EndOf(t));
            v.Audio.Seek(StartOf(t));
            active.Add(v);
            BusOf(bus).Mixer.AddMixerInput(v.Audio);
        }
        var f = FadeOf(t);
        v.Audio.Loop = t.Loop;
        v.Audio.Paused = false;
        v.Audio.Fader.FadeTo(1f, f.FadeInMs, f.Curve);
        ArmAutoFade(v);
        v.State = PlayState.Playing;
        return v;
    }

    /// <summary>With an end point set, fade out so the track reaches silence exactly there.</summary>
    private void ArmAutoFade(Voice v)
    {
        var f = FadeOf(v.Track);
        v.Audio.AutoFadeCurve = f.Curve;
        v.Audio.AutoFadeOutMs = v.Track.EndMs is null ? 0 : f.FadeOutMs;
    }

    private void StopVoice(Voice v)
    {
        var f = FadeOf(v.Track);
        active.Remove(v);
        draining.Add(v);
        v.Audio.AutoFadeOutMs = 0; // must not retarget the stop fade, or the voice would never end
        v.Audio.Paused = false;
        v.Audio.Fader.FadeTo(0f, f.FadeOutMs, f.Curve, endWhenDone: true);
    }

    private Voice? Find(Track t, BusKind bus) => active.Find(v => v.Track == t && v.Bus == bus);
    private static TimeSpan StartOf(Track t) => TimeSpan.FromMilliseconds(t.StartMs ?? 0);
    private static TimeSpan? EndOf(Track t) => t.EndMs is { } e ? TimeSpan.FromMilliseconds(e) : null;
    private OutputBus BusOf(BusKind bus) => bus == BusKind.Main ? Main : Monitor;
    private FadeSettings FadeOf(Track t) => new()
    {
        FadeInMs = t.FadeInMs ?? DefaultFade.FadeInMs,
        FadeOutMs = t.FadeOutMs ?? DefaultFade.FadeOutMs,
        Curve = DefaultFade.Curve,
    };
}
