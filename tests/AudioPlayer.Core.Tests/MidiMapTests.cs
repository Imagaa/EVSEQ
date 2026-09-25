using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Midi;
using AudioPlayer.Core.Model;

namespace AudioPlayer.Core.Tests;

public class MidiMapTests
{
    private static MidiMessage Note(int n, int vel, int ch = 1) => new(MidiKind.Note, ch, n, vel);
    private static MidiMessage Cc(int n, int v, int ch = 1) => new(MidiKind.ControlChange, ch, n, v);

    [Fact]
    public void DecodesRawShortMessages()
    {
        Assert.Equal(Note(60, 100, ch: 1), MidiMessage.FromRaw(0x00643C90));
        Assert.Equal(Note(60, 0, ch: 10), MidiMessage.FromRaw(0x00403C89));   // note off → value 0
        Assert.Equal(Cc(7, 127, ch: 2), MidiMessage.FromRaw(0x007F07B1));
        Assert.Null(MidiMessage.FromRaw(0x0000E0));                              // pitch bend ignored
    }

    [Fact]
    public void NoteOnFiresButtonNoteOffDoesNot()
    {
        var map = new MidiMap([new MidiBinding(MidiTarget.Panic, MidiKind.Note, 1, 36)]);
        Assert.Single(map.Handle(Note(36, 100)));
        Assert.Empty(map.Handle(Note(36, 0)));
    }

    [Fact]
    public void CcButtonFiresOncePerPress()
    {
        var map = new MidiMap([new MidiBinding(MidiTarget.MainStop, MidiKind.ControlChange, 1, 20)]);
        Assert.Single(map.Handle(Cc(20, 127)));
        Assert.Empty(map.Handle(Cc(20, 127)));   // held / repeated
        Assert.Empty(map.Handle(Cc(20, 0)));     // release
        Assert.Single(map.Handle(Cc(20, 127)));  // next press
    }

    [Fact]
    public void CcFaderFollowsEveryValueInDb()
    {
        var map = new MidiMap([new MidiBinding(MidiTarget.MainVolume, MidiKind.ControlChange, 1, 7)]);
        Assert.Equal(0, map.Handle(Cc(7, 127))[0].FaderDb!.Value, 3);
        Assert.Equal(Db.Floor, map.Handle(Cc(7, 0))[0].FaderDb!.Value, 3);
        Assert.Equal(-29.76, map.Handle(Cc(7, 64))[0].FaderDb!.Value, 2);
    }

    [Fact]
    public void OtherChannelIsIgnored()
    {
        var map = new MidiMap([new MidiBinding(MidiTarget.Panic, MidiKind.Note, 1, 36)]);
        Assert.Empty(map.Handle(Note(36, 100, ch: 2)));
    }

    [Fact]
    public void LearnReplacesTargetAndStealsControl()
    {
        var list = new List<MidiBinding>
        {
            new(MidiTarget.Panic, MidiKind.Note, 1, 36),
            new(MidiTarget.MainStop, MidiKind.Note, 1, 40),
        };

        MidiMap.Learn(list, MidiTarget.MainStop, null, Note(36, 100)); // pad 36 now means MainStop

        var only = Assert.Single(list);
        Assert.Equal(new MidiBinding(MidiTarget.MainStop, MidiKind.Note, 1, 36), only);
    }

    [Fact]
    public void PlayTrackBindingsAreKeptPerTrack()
    {
        var list = new List<MidiBinding>();
        MidiMap.Learn(list, MidiTarget.PlayTrack, 1, Note(50, 100));
        MidiMap.Learn(list, MidiTarget.PlayTrack, 2, Note(51, 100));
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void BindingsRoundTripThroughProjectFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ap-midi-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "p.approj");
        var p = new Project { MidiInputName = "nanoKONTROL2", MidiBindings = [new(MidiTarget.PlayTrack, MidiKind.Note, 10, 36, 3)] };

        ProjectSerializer.Save(p, file);
        var q = ProjectSerializer.Load(file);

        Assert.Equal("nanoKONTROL2", q.MidiInputName);
        Assert.Equal(p.MidiBindings, q.MidiBindings);
    }
}
