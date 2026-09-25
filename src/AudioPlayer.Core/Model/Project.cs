namespace AudioPlayer.Core.Model;

public sealed class Project
{
    public int Version { get; set; } = 1;
    public string? MainDeviceId { get; set; }
    public string? MonitorDeviceId { get; set; }
    public double MainVolumeDb { get; set; }
    public double MonitorVolumeDb { get; set; }
    public FadeSettings DefaultFade { get; set; } = new();
    public List<Track> Tracks { get; set; } = [];
    public List<ShortcutBinding> Shortcuts { get; set; } = DefaultShortcuts.Create();

    /// <summary>MIDI input port, matched by name (port indexes change when devices are replugged).</summary>
    public string? MidiInputName { get; set; }

    public List<Midi.MidiBinding> MidiBindings { get; set; } = [];
}
