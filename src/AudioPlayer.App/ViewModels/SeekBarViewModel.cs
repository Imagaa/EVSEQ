using AudioPlayer.Core.Engine;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioPlayer.App.ViewModels;

/// <summary>
/// Position slider for one bus. Follows the running voice; with no voice (cue bar, track only selected)
/// the thumb is a free cursor used to place start/end points.
/// </summary>
public sealed partial class SeekBarViewModel(PlayerEngine engine, BusKind bus) : ObservableObject
{
    private double position;

    public TrackViewModel? Target { get; private set; }

    public bool IsDragging { get; private set; }

    [ObservableProperty] public partial bool HasTrack { get; set; }
    [ObservableProperty] public partial string Title { get; set; } = "—";
    [ObservableProperty] public partial double Duration { get; set; } = 1;
    [ObservableProperty] public partial double RangeStart { get; set; }
    [ObservableProperty] public partial double RangeEnd { get; set; }
    [ObservableProperty] public partial string ElapsedText { get; set; } = "";
    [ObservableProperty] public partial string RemainingText { get; set; } = "";
    [ObservableProperty] public partial string RangeText { get; set; } = "";

    /// <summary>Seconds from file start. Set by the slider: seeks unless the thumb is being dragged.</summary>
    public double Position
    {
        get => position;
        set
        {
            position = value;
            if (!IsDragging) Seek();
            ShowTexts();
        }
    }

    public void BeginDrag() => IsDragging = true;

    public void EndDrag()
    {
        IsDragging = false;
        Seek();
    }

    /// <summary>Called by the UI timer.</summary>
    public void Update(TrackViewModel? t)
    {
        if (t != Target)
        {
            Target = t;
            IsDragging = false;
            position = t?.StartSeconds ?? 0;
        }
        HasTrack = t is not null;
        if (t is null)
        {
            Title = "—";
            Duration = 1;
            RangeStart = RangeEnd = 0;
            ElapsedText = RemainingText = RangeText = "";
            OnPropertyChanged(nameof(Position));
            return;
        }

        Title = t.Title;
        Duration = Math.Max(0.001, t.FileDuration.TotalSeconds);
        RangeStart = t.StartSeconds;
        RangeEnd = t.EndSeconds;
        RangeText = $"{MainViewModel.FmtPrecise(RangeStart)} – {MainViewModel.FmtPrecise(RangeEnd)}";
        if (!IsDragging && engine.GetPosition(t.Track, bus) is { } p) position = p.TotalSeconds;
        OnPropertyChanged(nameof(Position));
        ShowTexts();
    }

    private void Seek()
    {
        if (Target is not null) engine.Seek(Target.Track, bus, TimeSpan.FromSeconds(position));
    }

    private void ShowTexts()
    {
        if (Target is null) return;
        ElapsedText = MainViewModel.Fmt(TimeSpan.FromSeconds(position));
        RemainingText = "-" + MainViewModel.Fmt(TimeSpan.FromSeconds(Math.Max(0, RangeEnd - position)));
    }
}
