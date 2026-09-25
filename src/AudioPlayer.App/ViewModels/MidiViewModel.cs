using System.Collections.ObjectModel;
using System.Windows;
using AudioPlayer.Core.Midi;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPlayer.App.ViewModels;

/// <summary>One assignable function in the MIDI panel.</summary>
public sealed partial class MidiRowViewModel(MidiViewModel owner, MidiTarget target, string label, int? trackNumber = null) : ObservableObject
{
    public MidiTarget Target { get; } = target;
    public int? TrackNumber { get; } = trackNumber;
    public string Label { get; } = label;
    public bool IsFader => MidiMap.IsFader(Target);

    [ObservableProperty] public partial string AssignmentText { get; set; } = "—";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(LearnLabel))]
    public partial bool IsLearning { get; set; }

    public string LearnLabel => IsLearning ? "Gerakkan…" : "Learn";

    [RelayCommand] private void Learn() => owner.ToggleLearn(this);
    [RelayCommand] private void Clear() => owner.Clear(this);
}

/// <summary>MIDI controller input: port selection, MIDI learn, and turning messages into app actions.</summary>
public sealed partial class MidiViewModel : ObservableObject, IDisposable
{
    private readonly MainViewModel main;
    private readonly MidiInput input = new();
    private MidiMap map;
    private MidiRowViewModel? learning;

    public MidiViewModel(MainViewModel main)
    {
        this.main = main;
        map = new MidiMap(main.Project.MidiBindings);
        input.Received += m => Application.Current.Dispatcher.InvokeAsync(() => OnMessage(m));

        (MidiTarget, string)[] fixedRows =
        [
            (MidiTarget.MainPlayPause, "Play/Pause MAIN"), (MidiTarget.MainStop, "Stop MAIN"),
            (MidiTarget.CuePlayPause, "Play/Pause CUE"), (MidiTarget.CueStop, "Stop CUE"),
            (MidiTarget.Panic, "PANIC"),
            (MidiTarget.PlayPauseSelected, "Play/Pause track terpilih"), (MidiTarget.PreviewSelected, "Cue track terpilih"),
            (MidiTarget.StopSelected, "Stop track terpilih"),
            (MidiTarget.SelectPrevious, "Pilih track sebelumnya"), (MidiTarget.SelectNext, "Pilih track berikutnya"),
            (MidiTarget.MainVolume, "Fader master MAIN"), (MidiTarget.MonitorVolume, "Fader master MONITOR"),
            (MidiTarget.SelectedVolume, "Fader volume track terpilih"),
        ];
        foreach (var (target, label) in fixedRows) Rows.Add(new MidiRowViewModel(this, target, label));
        for (int n = 1; n <= 8; n++) Rows.Add(new MidiRowViewModel(this, MidiTarget.PlayTrack, $"Play track {n}", n));
    }

    public ObservableCollection<MidiRowViewModel> Rows { get; } = [];

    [ObservableProperty] public partial IReadOnlyList<string> Ports { get; set; } = [];
    [ObservableProperty] public partial string LastMessage { get; set; } = "—";
    [ObservableProperty] public partial bool IsConnected { get; set; }

    public string? SelectedPort
    {
        get => input.PortName;
        set
        {
            if (value is null || value == input.PortName) return;
            main.Project.MidiInputName = value;
            main.MarkDirty();
            Connect(value);
        }
    }

    /// <summary>Called whenever the main view model loads a project.</summary>
    public void ProjectLoaded()
    {
        CancelLearn();
        map = new MidiMap(main.Project.MidiBindings);
        RefreshPorts();
        Connect(main.Project.MidiInputName);
        RefreshRows();
    }

    public void RefreshPorts()
    {
        try
        {
            var ports = MidiInput.PortNames();
            if (!ports.SequenceEqual(Ports)) Ports = ports;
        }
        catch (NAudio.MmException)
        {
            Ports = [];
        }
        OnPropertyChanged(nameof(SelectedPort));
    }

    public void ToggleLearn(MidiRowViewModel row)
    {
        var wasThis = learning == row;
        CancelLearn();
        if (wasThis) return;
        learning = row;
        row.IsLearning = true;
    }

    public void Clear(MidiRowViewModel row)
    {
        main.Project.MidiBindings.RemoveAll(b => b.Target == row.Target && b.TrackNumber == row.TrackNumber);
        main.MarkDirty();
        RefreshRows();
    }

    public void Dispose() => input.Dispose();

    private void Connect(string? name)
    {
        IsConnected = input.Open(name);
        OnPropertyChanged(nameof(SelectedPort));
        if (name is null) return;
        if (IsConnected) main.Log($"MIDI terhubung: {name}", ActivityKind.Success);
        else main.Log($"MIDI \"{name}\" tidak ditemukan atau sedang dipakai aplikasi lain", ActivityKind.Warning);
    }

    private void CancelLearn()
    {
        if (learning is null) return;
        learning.IsLearning = false;
        learning = null;
    }

    private void RefreshRows()
    {
        foreach (var row in Rows)
        {
            var b = main.Project.MidiBindings.FirstOrDefault(x => x.Target == row.Target && x.TrackNumber == row.TrackNumber);
            row.AssignmentText = b is null ? "—" : $"{(b.Kind == MidiKind.Note ? "Note" : "CC")} {b.Number} · ch {b.Channel}";
        }
    }

    private void OnMessage(MidiMessage m)
    {
        LastMessage = m.ToString();

        // Learning takes the first press or knob movement; releases (note-off) are ignored.
        if (learning is { } row && !(m.Kind == MidiKind.Note && m.Value == 0))
        {
            MidiMap.Learn(main.Project.MidiBindings, row.Target, row.TrackNumber, m);
            CancelLearn();
            RefreshRows();
            main.MarkDirty();
            main.Log($"MIDI: {row.Label} ← {(m.Kind == MidiKind.Note ? "Note" : "CC")} {m.Number} · ch {m.Channel}", ActivityKind.Success);
            return;
        }

        foreach (var c in map.Handle(m)) Execute(c);
    }

    private void Execute(MidiCommand c)
    {
        switch (c.Binding.Target)
        {
            case MidiTarget.MainPlayPause: main.MainPlayPauseCommand.Execute(null); break;
            case MidiTarget.MainStop: main.MainStopCommand.Execute(null); break;
            case MidiTarget.CuePlayPause: main.CuePlayPauseCommand.Execute(null); break;
            case MidiTarget.CueStop: main.CueStopCommand.Execute(null); break;
            case MidiTarget.Panic: main.PanicCommand.Execute(null); break;
            case MidiTarget.PlayPauseSelected: main.PlayPauseCommand.Execute(null); break;
            case MidiTarget.PreviewSelected: main.PreviewCommand.Execute(null); break;
            case MidiTarget.StopSelected: main.StopCommand.Execute(null); break;
            case MidiTarget.SelectNext: main.SelectRelative(+1); break;
            case MidiTarget.SelectPrevious: main.SelectRelative(-1); break;
            case MidiTarget.PlayTrack when c.Binding.TrackNumber is { } n: main.PlayNumber(n); break;
            case MidiTarget.MainVolume: main.MainVolumeDb = c.FaderDb!.Value; break;
            case MidiTarget.MonitorVolume: main.MonitorVolumeDb = c.FaderDb!.Value; break;
            case MidiTarget.SelectedVolume when main.Selected is { } t: t.VolumeDb = c.FaderDb!.Value; break;
        }
    }
}
