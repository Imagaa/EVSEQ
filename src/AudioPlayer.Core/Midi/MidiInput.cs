using NAudio.Midi;

namespace AudioPlayer.Core.Midi;

/// <summary>One open MIDI input port (winmm), selected by name.</summary>
public sealed class MidiInput : IDisposable
{
    private MidiIn? port;

    public static IReadOnlyList<string> PortNames() =>
        Enumerable.Range(0, MidiIn.NumberOfDevices).Select(i => MidiIn.DeviceInfo(i).ProductName).ToList();

    public string? PortName { get; private set; }

    /// <summary>Raised on the MIDI driver thread for Note On/Off and CC messages.</summary>
    public event Action<MidiMessage>? Received;

    /// <summary>Opens the named port (closing any other). False when it is missing or busy.</summary>
    public bool Open(string? name)
    {
        Close();
        if (name is null) return false;
        int index = PortNames().ToList().IndexOf(name);
        if (index < 0) return false;
        try
        {
            var p = new MidiIn(index);
            p.MessageReceived += (_, e) =>
            {
                if (MidiMessage.FromRaw(e.RawMessage) is { } m) Received?.Invoke(m);
            };
            p.Start();
            port = p;
            PortName = name;
            return true;
        }
        catch (NAudio.MmException) // port in use by another app
        {
            return false;
        }
    }

    public void Close()
    {
        if (port is null) return;
        try
        {
            port.Stop();
        }
        catch (NAudio.MmException)
        {
            // device already unplugged
        }
        port.Dispose();
        port = null;
        PortName = null;
    }

    public void Dispose() => Close();
}
