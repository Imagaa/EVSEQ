using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Model;

namespace AudioPlayer.Core.Tests;

public class ProjectSerializerTests
{
    private static string NewDir()
    {
        var d = Path.Combine(Path.GetTempPath(), $"ap-proj-{Guid.NewGuid():N}");
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void RoundTripsAllFields()
    {
        var dir = NewDir();
        var audio = Path.Combine(dir, "a.wav");
        File.WriteAllBytes(audio, []);
        var p = new Project
        {
            MainDeviceId = "main-id",
            MonitorDeviceId = "mon-id",
            MainVolumeDb = -3,
            MonitorVolumeDb = -12,
            DefaultFade = new FadeSettings { FadeInMs = 250, FadeOutMs = 800, Curve = FadeCurve.Linear },
            Tracks =
            [
                new Track { FilePath = audio, Title = "Opening", VolumeDb = -6, Loop = true, Overlay = true,
                            Fade = new FadeSettings { FadeInMs = 50 } },
            ],
            Shortcuts = [new ShortcutBinding(ShortcutAction.PlayTrack, "Ctrl+Alt+D1", Global: true, TrackNumber: 1)],
        };
        var file = Path.Combine(dir, "show.approj");

        ProjectSerializer.Save(p, file);
        var q = ProjectSerializer.Load(file);

        Assert.Equal("main-id", q.MainDeviceId);
        Assert.Equal("mon-id", q.MonitorDeviceId);
        Assert.Equal(-3, q.MainVolumeDb);
        Assert.Equal(-12, q.MonitorVolumeDb);
        Assert.Equal(250, q.DefaultFade.FadeInMs);
        Assert.Equal(800, q.DefaultFade.FadeOutMs);
        Assert.Equal(FadeCurve.Linear, q.DefaultFade.Curve);
        var t = Assert.Single(q.Tracks);
        Assert.Equal(audio, t.FilePath);
        Assert.Equal("Opening", t.Title);
        Assert.Equal(-6, t.VolumeDb);
        Assert.True(t.Loop);
        Assert.True(t.Overlay);
        Assert.Equal(50, t.Fade!.FadeInMs);
        Assert.Equal(p.Shortcuts, q.Shortcuts);
        Assert.False(File.Exists(file + ".tmp"));
    }

    [Fact]
    public void ResolvesRelativePathAfterFolderMove()
    {
        var dir = NewDir();
        Directory.CreateDirectory(Path.Combine(dir, "audio"));
        var audio = Path.Combine(dir, "audio", "x.wav");
        File.WriteAllBytes(audio, []);
        ProjectSerializer.Save(new Project { Tracks = [new Track { FilePath = audio }] }, Path.Combine(dir, "p.approj"));

        var moved = dir + "-moved";
        Directory.Move(dir, moved);
        var q = ProjectSerializer.Load(Path.Combine(moved, "p.approj"));

        Assert.Equal(Path.Combine(moved, "audio", "x.wav"), q.Tracks[0].FilePath);
        Assert.False(q.Tracks[0].IsMissing);
    }

    [Fact]
    public void MissingFileDoesNotFailLoad()
    {
        var dir = NewDir();
        var file = Path.Combine(dir, "p.approj");
        ProjectSerializer.Save(new Project { Tracks = [new Track { FilePath = @"Z:\nope\gone.wav" }] }, file);

        var q = ProjectSerializer.Load(file);

        Assert.Equal(@"Z:\nope\gone.wav", q.Tracks[0].FilePath);
        Assert.True(q.Tracks[0].IsMissing);
    }

    [Fact]
    public void NewProjectHasDefaultShortcuts()
    {
        var p = new Project();
        Assert.Contains(p.Shortcuts, s => s.Action == ShortcutAction.Panic);
        Assert.Equal(9, p.Shortcuts.Count(s => s.Action == ShortcutAction.PlayTrack));
    }
}
