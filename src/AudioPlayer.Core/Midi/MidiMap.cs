using AudioPlayer.Core.Audio;

namespace AudioPlayer.Core.Midi;

public enum MidiKind { Note, ControlChange }

public enum MidiTarget
{
    MainPlayPause, MainStop, CuePlayPause, CueStop, Panic,
    PlayPauseSelected, PreviewSelected, StopSelected, SelectNext, SelectPrevious,
    PlayTrack,
    MainVolume, MonitorVolume, SelectedVolume,
}

/// <summary>A controller button/knob assigned to an action or fader. TrackNumber (1-based) only for PlayTrack.</summary>
public sealed record MidiBinding(MidiTarget Target, MidiKind Kind, int Channel, int Number, int? TrackNumber = null);

/// <summary>A decoded short MIDI message. Channel is 1-16.</summary>
public readonly record struct MidiMessage(MidiKind Kind, int Channel, int Number, int Value)
{
    /// <summary>Decodes a winmm short message; returns null for anything that is not Note On/Off or CC.</summary>
    public static MidiMessage? FromRaw(int raw)
    {
        int status = raw & 0xFF, data1 = (raw >> 8) & 0x7F, data2 = (raw >> 16) & 0x7F;
        int channel = (status & 0x0F) + 1;
        return (status & 0xF0) switch
        {
            0x90 => new MidiMessage(MidiKind.Note, channel, data1, data2),  // velocity 0 = note off
            0x80 => new MidiMessage(MidiKind.Note, channel, data1, 0),
            0xB0 => new MidiMessage(MidiKind.ControlChange, channel, data1, data2),
            _ => null,
        };
    }

    public override string ToString() => $"{(Kind == MidiKind.Note ? "Note" : "CC")} {Number} · ch {Channel} = {Value}";
}

/// <summary>A binding fired by a message; FaderDb is set for fader targets.</summary>
public readonly record struct MidiCommand(MidiBinding Binding, double? FaderDb);

/// <summary>Turns incoming messages into commands. Buttons fire once per press; faders follow every value.</summary>
public sealed class MidiMap(List<MidiBinding> bindings)
{
    private readonly Dictionary<(MidiKind, int, int), int> lastValue = [];

    public static bool IsFader(MidiTarget t) => t is MidiTarget.MainVolume or MidiTarget.MonitorVolume or MidiTarget.SelectedVolume;

    /// <summary>0..127 onto the fader range, linear in dB (0 = silence floor, 127 = 0 dB).</summary>
    public static double ToDb(int value) => Db.Floor + Math.Clamp(value, 0, 127) / 127.0 * -Db.Floor;

    public List<MidiCommand> Handle(MidiMessage m)
    {
        var key = (m.Kind, m.Channel, m.Number);
        lastValue.TryGetValue(key, out int previous);
        lastValue[key] = m.Value;
        // A press: note-on, or a CC crossing the middle upward (controllers send 127 on press, 0 on release).
        bool pressed = m.Kind == MidiKind.Note ? m.Value > 0 : m.Value >= 64 && previous < 64;

        var commands = new List<MidiCommand>();
        foreach (var b in bindings)
        {
            if (b.Kind != m.Kind || b.Channel != m.Channel || b.Number != m.Number) continue;
            if (IsFader(b.Target))
            {
                if (m.Kind == MidiKind.ControlChange) commands.Add(new MidiCommand(b, ToDb(m.Value)));
            }
            else if (pressed)
            {
                commands.Add(new MidiCommand(b, null));
            }
        }
        return commands;
    }

    /// <summary>
    /// Assigns the message's control to the target. The target loses its old control, and the control
    /// is taken away from any other target so one knob never does two things.
    /// </summary>
    public static MidiBinding Learn(List<MidiBinding> bindings, MidiTarget target, int? trackNumber, MidiMessage m)
    {
        bindings.RemoveAll(b => (b.Target == target && b.TrackNumber == trackNumber)
                                || (b.Kind == m.Kind && b.Channel == m.Channel && b.Number == m.Number));
        var binding = new MidiBinding(target, m.Kind, m.Channel, m.Number, trackNumber);
        bindings.Add(binding);
        return binding;
    }
}
