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

    /// <summary>
    /// Bound to the seek bar's IsScrubbing: while true the bar stops following playback;
    /// turning false performs the seek to where the user let go.
    /// </summary>
    public bool IsDragging
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            if (!value) Seek();
        }
    }

    // Envelope drawn on the seek bar (seconds)
    [ObservableProperty] public partial double FadeIn { get; set; }
    [ObservableProperty] public partial double FadeOut { get; set; }
    [ObservableProperty] public partial bool ShowFadeOut { get; set; }
    [ObservableProperty] public partial AudioPlayer.Core.Audio.FadeCurve Curve { get; set; }
    [ObservableProperty] public partial float[]? Peaks { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(RangeText))] public partial bool HasTrack { get; set; }
    [ObservableProperty] public partial string Title { get; set; } = "—";
    [ObservableProperty] public partial double Duration { get; set; } = 1;
    // Start/end points (seconds). The seek bar writes them while a marker is dragged.
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RangeText))] public partial double RangeStart { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RangeText))] public partial double RangeEnd { get; set; }
    [ObservableProperty] public partial string ElapsedText { get; set; } = "";
    [ObservableProperty] public partial string RemainingText { get; set; } = "";

    public string RangeText => HasTrack ? $"{MainViewModel.FmtPrecise(RangeStart)} – {MainViewModel.FmtPrecise(RangeEnd)}" : "";

    /// <summary>
    /// Bound to the seek bar's IsEditingRange: while true the markers follow the mouse instead of the track;
    /// turning false saves the new start/end on the track.
    /// </summary>
    public bool IsEditingRange
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
            if (!value) CommitRange();
        }
    }

    private void CommitRange()
    {
        if (Target is null) return;
        double s = Math.Clamp(RangeStart, 0, Duration), e = Math.Clamp(RangeEnd, 0, Duration);
        // Snap to "no point" at the file edges so the track keeps following its full length.
        int? startMs = s < 0.05 ? null : (int)Math.Round(s * 1000);
        int? endMs = e > Duration - 0.05 ? null : (int)Math.Round(e * 1000);
        Target.SetRange(startMs, endMs);
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(PlayLabel))]
    public partial bool IsPlaying { get; set; }

    public string PlayLabel => IsPlaying ? "⏸" : "▶";

    /// <summary>True when the thumb is a free cursor away from the start point (no voice running).</summary>
    public bool CursorMoved => Target is not null && Math.Abs(position - Target.StartSeconds) > 0.05;

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

    /// <summary>Called by the UI timer.</summary>
    public void Update(TrackViewModel? t)
    {
        if (t != Target)
        {
            if (IsDragging || IsEditingRange)
            {
                Target = null; // switching tracks mid-drag: end the drag without touching either track
                IsDragging = false;
                IsEditingRange = false;
            }
            Target = t;
            position = t?.StartSeconds ?? 0;
        }
        HasTrack = t is not null;
        IsPlaying = t is not null && engine.GetState(t.Track, bus) == PlayState.Playing;
        if (t is null)
        {
            Title = "—";
            Duration = 1;
            RangeStart = RangeEnd = 0;
            ElapsedText = RemainingText = "";
            Peaks = null;
            OnPropertyChanged(nameof(Position));
            OnPropertyChanged(nameof(RangeText));
            return;
        }

        Title = t.Title;
        Duration = Math.Max(0.001, t.FileDuration.TotalSeconds);
        if (!IsEditingRange)
        {
            RangeStart = t.StartSeconds;
            RangeEnd = t.EndSeconds;
        }
        FadeIn = t.FadeInSeconds;
        FadeOut = t.FadeOutSeconds;
        Peaks = t.Peaks;
        // The engine fades out before the end (end point or end of file), except when looping.
        ShowFadeOut = !t.Loop;
        Curve = t.Curve;
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
