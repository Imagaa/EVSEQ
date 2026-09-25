using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Engine;
using AudioPlayer.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPlayer.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };

    public MainViewModel()
    {
        Engine = new PlayerEngine(new OutputBus(), new OutputBus());
        MainBar = new SeekBarViewModel(Engine, BusKind.Main);
        CueBar = new SeekBarViewModel(Engine, BusKind.Monitor);
        Devices.DeviceUnavailable += id =>
            Application.Current.Dispatcher.InvokeAsync(() => OnDeviceUnavailable(id));
        Engine.Main.PlaybackFailed += (_, ex) => BusFailed(BusKind.Main, ex.Message);
        Engine.Monitor.PlaybackFailed += (_, ex) => BusFailed(BusKind.Monitor, ex.Message);
        Tracks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowWelcome));
            NotifyShortcutsChanged(); // per-track shortcuts follow their tracks
        };
        Load(new Project(), null);
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    public PlayerEngine Engine { get; }
    public SeekBarViewModel MainBar { get; }
    public SeekBarViewModel CueBar { get; }
    public AudioDevices Devices { get; } = new();
    public Project Project { get; private set; } = new();
    public string? ProjectPath { get; private set; }
    public ObservableCollection<TrackViewModel> Tracks { get; } = [];

    /// <summary>The welcome screen replaces the empty track list.</summary>
    public bool ShowWelcome => Tracks.Count == 0;

    [ObservableProperty] public partial TrackViewModel? Selected { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial string TotalText { get; set; } = "00:00";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SaveStateText))]
    public partial bool IsDirty { get; set; }

    [ObservableProperty] public partial string? Alert { get; set; }

    // ---- Visual indicators: activity log, save/autosave state, devices ----

    /// <summary>Newest first; every status message, alert, save and autosave lands here.</summary>
    public ObservableCollection<ActivityEntry> Activity { get; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SaveStateText))]
    public partial DateTime? LastSavedAt { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(AutosaveText))]
    public partial DateTime? LastAutosaveAt { get; set; }

    public string SaveStateText =>
        IsDirty ? "● Belum disimpan"
        : LastSavedAt is { } t ? $"✓ Tersimpan {t:HH:mm:ss}"
        : ProjectPath is null ? "Project baru"
        : "✓ Tersimpan";

    public string AutosaveText => LastAutosaveAt is { } a ? $"Autosave {a:HH:mm:ss}" : "Autosave aktif (60 dtk)";

    [ObservableProperty] public partial string MainDeviceText { get; set; } = "";
    [ObservableProperty] public partial bool MainDeviceOk { get; set; }
    [ObservableProperty] public partial string MonitorDeviceText { get; set; } = "";
    [ObservableProperty] public partial bool MonitorDeviceOk { get; set; }

    public void Log(string text, ActivityKind kind = ActivityKind.Info)
    {
        Activity.Insert(0, new ActivityEntry(DateTime.Now, text, kind));
        if (Activity.Count > 300) Activity.RemoveAt(Activity.Count - 1);
    }

    partial void OnStatusChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) Log(value, ActivityKind.Warning);
    }

    partial void OnAlertChanged(string? value)
    {
        if (value is not null) Log(value, ActivityKind.Error);
    }

    public void NoteSaved(string path)
    {
        MarkSaved(path);
        LastSavedAt = DateTime.Now;
        Log($"Project disimpan: {Path.GetFileName(path)}", ActivityKind.Success);
    }

    public void NoteAutosaved()
    {
        LastAutosaveAt = DateTime.Now;
        Log("Autosave (perubahan belum disimpan diamankan)");
    }

    private void UpdateDeviceInfo()
    {
        IReadOnlyList<AudioDevice> devices;
        try
        {
            devices = Devices.RenderDevices();
        }
        catch (COMException)
        {
            devices = [];
        }
        string Name(string? id) => devices.FirstOrDefault(d => d.Id == id)?.Name ?? "?";
        if (!devices.Select(d => d.Id).SequenceEqual(OutputDevices.Select(d => d.Id))) OutputDevices = devices;
        OnPropertyChanged(nameof(MainOutputId));
        OnPropertyChanged(nameof(MonitorOutputId));

        MainDeviceOk = Engine.Main.DeviceId is not null;
        MainDeviceText = MainDeviceOk ? Name(Engine.Main.DeviceId) : "tidak terhubung";
        MonitorDeviceOk = Engine.Monitor.DeviceId is not null;
        MonitorDeviceText = MonitorDeviceOk ? Name(Engine.Monitor.DeviceId) : "belum dipilih";
    }

    public double MainVolumeDb
    {
        get => Project.MainVolumeDb;
        set { Project.MainVolumeDb = value; Engine.Main.VolumeDb = value; OnPropertyChanged(); IsDirty = true; }
    }

    public double MonitorVolumeDb
    {
        get => Project.MonitorVolumeDb;
        set { Project.MonitorVolumeDb = value; Engine.Monitor.VolumeDb = value; OnPropertyChanged(); IsDirty = true; }
    }

    public static string Fmt(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");

    /// <summary>Tenths of a second, for start/end points.</summary>
    public static string FmtPrecise(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.f");

    public void MarkDirty() => IsDirty = true;

    public void RangesChanged() => Renumber();

    // Start/end points are set on the cue bar's track at the cue bar's position.
    [RelayCommand]
    private void SetStart()
    {
        if (CueBar.Target is not { } t) return;
        var ms = (int)Math.Round(CueBar.Position * 1000);
        if (t.Track.EndMs is { } end && ms >= end)
        {
            Status = "Start point harus sebelum end point.";
            return;
        }
        t.SetRange(ms == 0 ? null : ms, t.Track.EndMs);
    }

    [RelayCommand]
    private void SetEnd()
    {
        if (CueBar.Target is not { } t) return;
        var ms = (int)Math.Round(CueBar.Position * 1000);
        if (ms <= (t.Track.StartMs ?? 0))
        {
            Status = "End point harus sesudah start point.";
            return;
        }
        t.SetRange(t.Track.StartMs, ms >= (int)t.FileDuration.TotalMilliseconds ? null : ms);
    }

    [RelayCommand] private void ResetRange() => CueBar.Target?.SetRange(null, null);

    // Transport on the seek bars. Main falls back to the selected track when nothing is on Main.
    [RelayCommand]
    private void MainPlayPause() => Run(MainBar.Target ?? Selected, x =>
    {
        if (Engine.GetState(x.Track, BusKind.Main) == PlayState.Playing) Engine.Pause(x.Track, BusKind.Main);
        else Engine.Play(x.Track);
    });

    [RelayCommand]
    private void MainStop()
    {
        if (MainBar.Target is { } t) Engine.Stop(t.Track, BusKind.Main);
    }

    /// <summary>From stopped, the preview starts where the cue cursor is.</summary>
    [RelayCommand]
    private void CuePlayPause() => Run(CueBar.Target ?? Selected, x =>
    {
        switch (Engine.GetState(x.Track, BusKind.Monitor))
        {
            case PlayState.Playing:
                Engine.Pause(x.Track, BusKind.Monitor);
                break;
            case PlayState.Paused:
                Engine.Preview(x.Track); // resumes
                break;
            default:
                var from = CueBar.Target == x && CueBar.CursorMoved ? CueBar.Position : (double?)null;
                Engine.Preview(x.Track);
                if (from is { } s) Engine.Seek(x.Track, BusKind.Monitor, TimeSpan.FromSeconds(s));
                break;
        }
    });

    [RelayCommand]
    private void CueStop()
    {
        if (CueBar.Target is { } t) Engine.Stop(t.Track, BusKind.Monitor);
    }

    // Keys for the seek-bar transport, edited in place next to the buttons. Cleared keys are kept
    // as an empty binding so loading the project does not re-add the default.
    public string MainPlayPauseKey { get => KeyOf(ShortcutAction.MainPlayPause); set => SetKey(ShortcutAction.MainPlayPause, value); }
    public string MainStopKey { get => KeyOf(ShortcutAction.MainStop); set => SetKey(ShortcutAction.MainStop, value); }
    public string CuePlayPauseKey { get => KeyOf(ShortcutAction.CuePlayPause); set => SetKey(ShortcutAction.CuePlayPause, value); }
    public string CueStopKey { get => KeyOf(ShortcutAction.CueStop); set => SetKey(ShortcutAction.CueStop, value); }

    private string KeyOf(ShortcutAction action) => Project.Shortcuts.FirstOrDefault(s => s.Action == action)?.Gesture ?? "";

    private void SetKey(ShortcutAction action, string gesture)
    {
        Project.Shortcuts.RemoveAll(s => s.Action == action);
        Project.Shortcuts.Add(new ShortcutBinding(action, gesture ?? ""));
        MarkDirty();
        RefreshKeys();
        NotifyShortcutsChanged();
    }

    private void RefreshKeys()
    {
        OnPropertyChanged(nameof(MainPlayPauseKey));
        OnPropertyChanged(nameof(MainStopKey));
        OnPropertyChanged(nameof(CuePlayPauseKey));
        OnPropertyChanged(nameof(CueStopKey));
    }

    public string WindowTitle =>
        $"{(ProjectPath is null ? "Tanpa judul" : Path.GetFileNameWithoutExtension(ProjectPath))}{(IsDirty ? " *" : "")} — Audio Player";

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));

    public string ProjectName => ProjectPath is null ? "Tanpa judul" : Path.GetFileNameWithoutExtension(ProjectPath);

    public void MarkSaved(string path)
    {
        ProjectPath = path;
        IsDirty = false;
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(SaveStateText));
    }

    public void Load(Project project, string? path)
    {
        Engine.Panic();
        Project = project;
        ProjectPath = path;
        Engine.DefaultFade = project.DefaultFade;
        Engine.Main.VolumeDb = project.MainVolumeDb;
        Engine.Monitor.VolumeDb = project.MonitorVolumeDb;
        Tracks.Clear();
        foreach (var t in project.Tracks) Tracks.Add(new TrackViewModel(t, this));
        Renumber();
        Alert = null;
        ApplyDevices();
        OnPropertyChanged(nameof(MainVolumeDb));
        OnPropertyChanged(nameof(MonitorVolumeDb));
        IsDirty = false;
        LastSavedAt = null;
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(SaveStateText));
        RefreshKeys();
        Log(path is null ? "Project baru" : $"Project dibuka: {Path.GetFileName(path)}");
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            var t = new Track { FilePath = p, Title = Path.GetFileNameWithoutExtension(p) };
            Project.Tracks.Add(t);
            Tracks.Add(new TrackViewModel(t, this));
        }
        Renumber();
        IsDirty = true;
    }

    public void ApplyDevices()
    {
        var notes = new List<string>();
        AttachMain(notes);
        AttachMonitor(notes);
        Status = string.Join("  ", notes);
        UpdateDeviceInfo();
    }

    private void AttachMain(List<string> notes)
    {
        try
        {
            var main = Devices.Find(Project.MainDeviceId);
            if (main is null && Project.MainDeviceId is not null) notes.Add("Device Main tersimpan tidak ditemukan — memakai device default.");
            Engine.Main.AttachDevice(main ?? Devices.Default());
        }
        catch (COMException ex)
        {
            notes.Add($"Gagal membuka device Main: {ex.Message}");
        }
    }

    private void AttachMonitor(List<string> notes)
    {
        try
        {
            Engine.Monitor.AttachDevice(Devices.Find(Project.MonitorDeviceId));
        }
        catch (COMException ex)
        {
            notes.Add($"Gagal membuka device Monitor: {ex.Message}");
        }
        // Monitor never falls back to the default device: preview must not leak to the audience.
        if (Engine.Monitor.DeviceId is null) notes.Add("Device Monitor belum dipilih — pilih di panel Master.");
    }

    // ---- Output selection from the Master panel (one bus at a time, the other keeps playing) ----

    [ObservableProperty] public partial IReadOnlyList<AudioDevice> OutputDevices { get; set; } = [];

    public string? MainOutputId
    {
        get => Engine.Main.DeviceId;
        set
        {
            if (value is null || value == Engine.Main.DeviceId) return;
            Project.MainDeviceId = value;
            var notes = new List<string>();
            AttachMain(notes);
            AfterOutputChange(notes, "MAIN", Engine.Main.DeviceId);
        }
    }

    public string? MonitorOutputId
    {
        get => Engine.Monitor.DeviceId;
        set
        {
            if (value is null || value == Engine.Monitor.DeviceId) return;
            Project.MonitorDeviceId = value;
            var notes = new List<string>();
            AttachMonitor(notes);
            AfterOutputChange(notes, "MONITOR", Engine.Monitor.DeviceId);
        }
    }

    /// <summary>Re-reads the device list, e.g. when a dropdown opens after plugging a USB interface.</summary>
    public void RefreshOutputDevices() => UpdateDeviceInfo();

    private void AfterOutputChange(List<string> notes, string bus, string? id)
    {
        if (Engine.Main.DeviceId is not null && Engine.Main.DeviceId == Engine.Monitor.DeviceId)
            notes.Add("Main dan Monitor memakai device yang sama — preview akan terdengar oleh audiens.");
        Status = string.Join("  ", notes);
        MarkDirty();
        UpdateDeviceInfo();
        if (id is not null) Log($"Output {bus}: {OutputDevices.FirstOrDefault(d => d.Id == id)?.Name ?? id}", ActivityKind.Success);
    }

    /// <summary>Raised when any shortcut source changes (track shortcut edited, tracks added/removed).</summary>
    public event Action? ShortcutsChanged;

    public void NotifyShortcutsChanged() => ShortcutsChanged?.Invoke();

    public void PlayNumber(int number)
    {
        if (number >= 1 && number <= Tracks.Count) PlayTrack(Tracks[number - 1]);
    }

    public void PlayTrack(TrackViewModel t)
    {
        Selected = t;
        Run(t, x => Engine.Play(x.Track));
    }

    public void ApplySettings(string? mainDeviceId, string? monitorDeviceId, FadeSettings fade)
    {
        bool devicesChanged = mainDeviceId != Project.MainDeviceId || monitorDeviceId != Project.MonitorDeviceId;
        Project.MainDeviceId = mainDeviceId;
        Project.MonitorDeviceId = monitorDeviceId;
        Project.DefaultFade = fade;
        Engine.DefaultFade = fade;
        foreach (var t in Tracks) t.DefaultsChanged();
        RefreshKeys(); // the settings dialog may have edited the shortcut table
        if (devicesChanged)
        {
            Alert = null;
            ApplyDevices();
        }
        IsDirty = true;
    }

    [RelayCommand]
    private void UseDefaultForMain()
    {
        try
        {
            Engine.Main.AttachDevice(Devices.Default());
            Alert = null;
            Log("Main dipindah ke device default", ActivityKind.Success);
        }
        catch (COMException ex)
        {
            Alert = $"Gagal membuka device default: {ex.Message}";
        }
        UpdateDeviceInfo();
    }

    [RelayCommand] private void DismissAlert() => Alert = null;

    private void OnDeviceUnavailable(string id)
    {
        if (id == Engine.Main.DeviceId) BusFailed(BusKind.Main, "device dicabut/nonaktif");
        else if (id == Engine.Monitor.DeviceId) BusFailed(BusKind.Monitor, "device dicabut/nonaktif");
    }

    private void BusFailed(BusKind bus, string reason)
    {
        var b = bus == BusKind.Main ? Engine.Main : Engine.Monitor;
        if (b.DeviceId is null) return; // already reported
        b.DetachDevice(); // voices stay in the mixer and resume where they were once a device is attached
        Alert = bus == BusKind.Main
            ? $"⚠ Output MAIN terputus ({reason}). Tekan 'Pakai device default' atau pilih device di Settings."
            : $"⚠ Output MONITOR terputus ({reason}). Pilih device baru di Settings.";
        UpdateDeviceInfo();
    }

    [RelayCommand]
    private void PlayPause(TrackViewModel? t) => Run(t, x =>
    {
        if (Engine.GetState(x.Track, BusKind.Main) == PlayState.Playing) Engine.Pause(x.Track, BusKind.Main);
        else Engine.Play(x.Track);
    });

    [RelayCommand] private void Stop(TrackViewModel? t) => Run(t, x => Engine.Stop(x.Track, BusKind.Main));

    [RelayCommand] private void Preview(TrackViewModel? t) => Run(t, x => Engine.Preview(x.Track));

    [RelayCommand]
    private void Panic()
    {
        Engine.Panic();
        Status = "PANIC — semua track dihentikan.";
    }

    [RelayCommand]
    private void Remove(TrackViewModel? t) => Run(t, x =>
    {
        Engine.Stop(x.Track, BusKind.Main);
        Engine.Stop(x.Track, BusKind.Monitor);
        Project.Tracks.Remove(x.Track);
        Tracks.Remove(x);
        Renumber();
        IsDirty = true;
    });

    [RelayCommand] private void MoveUp(TrackViewModel? t) => Move(t, -1);
    [RelayCommand] private void MoveDown(TrackViewModel? t) => Move(t, +1);

    public void Dispose()
    {
        timer.Stop();
        Engine.Dispose();
        Engine.Main.Dispose();
        Engine.Monitor.Dispose();
        Devices.Dispose();
    }

    private void Move(TrackViewModel? t, int delta)
    {
        t ??= Selected;
        if (t is null) return;
        int i = Tracks.IndexOf(t), j = i + delta;
        if (j < 0 || j >= Tracks.Count) return;
        Tracks.Move(i, j);
        Project.Tracks.RemoveAt(i);
        Project.Tracks.Insert(j, t.Track);
        Renumber();
        Selected = t;
        IsDirty = true;
    }

    private void Run(TrackViewModel? t, Action<TrackViewModel> action)
    {
        t ??= Selected;
        if (t is null) return;
        try
        {
            action(t);
            Status = "";
        }
        catch (Exception ex)
        {
            Status = $"{t.Title}: {ex.Message}";
        }
    }

    private void Renumber()
    {
        for (int i = 0; i < Tracks.Count; i++) Tracks[i].Number = i + 1;
        TotalText = Fmt(TimeSpan.FromTicks(Tracks.Sum(t => t.Duration.Ticks)));
    }

    private void Tick()
    {
        Engine.Pump();
        foreach (var t in Tracks)
        {
            t.MainState = Engine.GetState(t.Track, BusKind.Main);
            t.MonitorState = Engine.GetState(t.Track, BusKind.Monitor);
            var rem = Engine.GetRemaining(t.Track, BusKind.Main) ?? Engine.GetRemaining(t.Track, BusKind.Monitor);
            t.RemainingText = rem is { } r ? "-" + Fmt(r) : "";
        }

        // Main bar: the playlist track (not an overlay jingle) first, else whatever is on Main.
        MainBar.Update(
            Tracks.FirstOrDefault(t => t.MainState == PlayState.Playing && !t.Overlay)
            ?? Tracks.FirstOrDefault(t => t.MainState == PlayState.Paused && !t.Overlay)
            ?? Tracks.FirstOrDefault(t => t.MainState != PlayState.Stopped));
        // Cue bar: the track previewing on Monitor, else the selected track (free cursor for start/end).
        CueBar.Update(Tracks.FirstOrDefault(t => t.MonitorState != PlayState.Stopped) ?? Selected);
    }
}
