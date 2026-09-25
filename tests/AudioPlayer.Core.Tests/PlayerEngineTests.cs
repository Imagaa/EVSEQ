using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Engine;
using AudioPlayer.Core.Model;

namespace AudioPlayer.Core.Tests;

public class PlayerEngineTests : IDisposable
{
    private readonly OutputBus main = new(), monitor = new();
    private readonly PlayerEngine engine;

    public PlayerEngineTests()
    {
        engine = new PlayerEngine(main, monitor)
        {
            DefaultFade = new FadeSettings { FadeInMs = 10, FadeOutMs = 10, Curve = FadeCurve.Linear },
        };
    }

    public void Dispose() => engine.Dispose();

    private static Track NewTrack(double seconds = 2) =>
        new() { FilePath = TestAudio.CreateWav(48000, 2, seconds), Title = "t" };

    private float[] Pull(OutputBus bus, int ms)
    {
        var b = TestAudio.Read(bus.Output, 48000 * 2 * ms / 1000);
        engine.Pump();
        return b;
    }

    /// <summary>Pulls both buses for the same duration, as the two real devices would.</summary>
    private (float[] Main, float[] Monitor) PullBoth(int ms) => (Pull(main, ms), Pull(monitor, ms));

    private static float Rms(float[] b) => MathF.Sqrt(b.Select(x => x * x).Average());

    [Fact]
    public void PlayGoesToMainOnly()
    {
        engine.Play(NewTrack());
        var (m, mon) = PullBoth(100);
        Assert.True(Rms(m[^960..]) > 0.1f);
        Assert.Equal(0f, Rms(mon));
    }

    [Fact]
    public void PreviewGoesToMonitorOnly()
    {
        engine.Preview(NewTrack());
        var (m, mon) = PullBoth(100);
        Assert.Equal(0f, Rms(m));
        Assert.True(Rms(mon[^960..]) > 0.1f);
    }

    [Fact]
    public void PauseHoldsPositionAndResumes()
    {
        var t = NewTrack();
        engine.Play(t);
        Pull(main, 200);
        engine.Pause(t, BusKind.Main);
        Pull(main, 100); // fade-out completes
        var held = engine.GetPosition(t, BusKind.Main);

        var silent = Pull(main, 200);

        Assert.Equal(PlayState.Paused, engine.GetState(t, BusKind.Main));
        Assert.Equal(held, engine.GetPosition(t, BusKind.Main));
        Assert.Equal(0f, Rms(silent));

        engine.Play(t);
        Assert.True(Rms(Pull(main, 100)[^960..]) > 0.1f);
        Assert.Equal(PlayState.Playing, engine.GetState(t, BusKind.Main));
    }

    [Fact]
    public void StopFadesOutAndReleases()
    {
        var t = NewTrack();
        engine.Play(t);
        Pull(main, 100);
        engine.Stop(t, BusKind.Main);
        Assert.Equal(PlayState.Stopped, engine.GetState(t, BusKind.Main));
        Pull(main, 100);
        Assert.Equal(0f, Rms(Pull(main, 50)));
    }

    [Fact]
    public void PlayingNonOverlayReplacesCurrentMainTrack()
    {
        Track a = NewTrack(), b = NewTrack();
        engine.Play(a);
        Pull(main, 50);
        engine.Play(b);
        Assert.Equal(PlayState.Stopped, engine.GetState(a, BusKind.Main));
        Assert.Equal(PlayState.Playing, engine.GetState(b, BusKind.Main));
    }

    [Fact]
    public void OverlayPlaysOnTopOfCurrentMainTrack()
    {
        Track a = NewTrack(), jingle = NewTrack();
        jingle.Overlay = true;
        engine.Play(a);
        engine.Play(jingle);
        Assert.Equal(PlayState.Playing, engine.GetState(a, BusKind.Main));
        Assert.Equal(PlayState.Playing, engine.GetState(jingle, BusKind.Main));
    }

    [Fact]
    public void PreviewIsExclusiveAndToggles()
    {
        Track a = NewTrack(), b = NewTrack();
        engine.Preview(a);
        engine.Preview(b);
        Assert.Equal(PlayState.Stopped, engine.GetState(a, BusKind.Monitor));
        Assert.Equal(PlayState.Playing, engine.GetState(b, BusKind.Monitor));
        engine.Preview(b);
        Assert.Equal(PlayState.Stopped, engine.GetState(b, BusKind.Monitor));
    }

    [Fact]
    public void NaturalEndRaisesTrackEnded()
    {
        var t = NewTrack(0.1);
        (Track, BusKind)? ended = null;
        engine.TrackEnded += (tr, bus) => ended = (tr, bus);
        engine.Play(t);
        Pull(main, 300);
        Assert.NotNull(ended);
        Assert.Equal((t, BusKind.Main), ended.Value);
        Assert.Equal(PlayState.Stopped, engine.GetState(t, BusKind.Main));
    }

    [Fact]
    public void StopDoesNotRaiseTrackEnded()
    {
        var t = NewTrack(0.1);
        var raised = false;
        engine.TrackEnded += (_, _) => raised = true;
        engine.Play(t);
        engine.Stop(t, BusKind.Main);
        Pull(main, 300);
        Assert.False(raised);
    }

    [Fact]
    public void PanicSilencesBothBusesInstantly()
    {
        Track a = NewTrack(), b = NewTrack();
        engine.Play(a);
        engine.Preview(b);
        PullBoth(100);
        engine.Panic();
        var (m, mon) = PullBoth(10);
        Assert.Equal(0f, Rms(m));
        Assert.Equal(0f, Rms(mon));
        Assert.Equal(PlayState.Stopped, engine.GetState(a, BusKind.Main));
        Assert.Equal(PlayState.Stopped, engine.GetState(b, BusKind.Monitor));
    }

    [Fact]
    public void FailedPlayKeepsCurrentTrackRunning()
    {
        var a = NewTrack();
        engine.Play(a);
        Assert.ThrowsAny<Exception>(() => engine.Play(new Track { FilePath = @"Z:\missing.wav" }));
        Assert.Equal(PlayState.Playing, engine.GetState(a, BusKind.Main));
    }

    [Fact]
    public void PerTrackFadeOverridesDefault()
    {
        Track slow = NewTrack(), normal = NewTrack();
        slow.FadeInMs = 1000; // default in these tests is 10 ms
        engine.Play(normal);
        var fast = Rms(Pull(main, 100)[^960..]);
        engine.Panic();

        engine.Play(slow);
        var ramping = Rms(Pull(main, 100)[^960..]);

        Assert.True(ramping < fast * 0.5f, $"slow fade {ramping} should be well below {fast}");
    }

    [Fact]
    public void PlayStartsAtStartPointAndEndsAtEndPoint()
    {
        var t = NewTrack(2);
        t.StartMs = 500;
        t.EndMs = 700;
        var ended = false;
        engine.TrackEnded += (_, _) => ended = true;

        engine.Play(t);
        Assert.InRange(engine.GetPosition(t, BusKind.Main)!.Value.TotalMilliseconds, 499, 501);
        Pull(main, 300); // 200 ms range is exhausted
        Assert.True(ended);
    }

    [Fact]
    public void StopDuringAutoFadeStillReleasesVoice()
    {
        var t = NewTrack(2);
        t.EndMs = 300;
        engine.DefaultFade.FadeOutMs = 200;
        engine.Play(t);
        Pull(main, 150);           // inside the 200 ms auto fade zone
        engine.Stop(t, BusKind.Main);
        Pull(main, 400);
        Assert.Empty(main.Mixer.MixerInputs);
    }

    [Fact]
    public void SeekMovesRunningVoice()
    {
        var t = NewTrack(2);
        engine.Preview(t);
        engine.Seek(t, BusKind.Monitor, TimeSpan.FromMilliseconds(1500));
        Assert.InRange(engine.GetRemaining(t, BusKind.Monitor)!.Value.TotalMilliseconds, 499, 501);
    }

    [Fact]
    public void RefreshAppliesNewEndPointLive()
    {
        var t = NewTrack(2);
        engine.Play(t);
        Pull(main, 100);
        t.EndMs = 200;
        engine.Refresh(t);
        Pull(main, 200);
        Assert.Equal(PlayState.Stopped, engine.GetState(t, BusKind.Main));
    }

    [Fact]
    public void RefreshAppliesVolumeLive()
    {
        var t = NewTrack();
        engine.Play(t);
        Pull(main, 100);
        t.VolumeDb = Db.Floor;
        engine.Refresh(t);
        Pull(main, 20); // one ramp buffer
        Assert.Equal(0f, Rms(Pull(main, 20)));
    }
}
