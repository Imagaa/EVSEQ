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

    [ObservableProperty] public partial bool HasTrack { get; set; }
    [ObservableProperty] public partial string Title { get; set; } = "—";
    [ObservableProperty] public partial double Duration { get; set; } = 1;
    [ObservableProperty] public partial double RangeStart { get; set; }
    [ObservableProperty] public partial double RangeEnd { get; set; }
    [ObservableProperty] public partial string ElapsedText { get; set; } = "";
    [ObservableProperty] public partial string RemainingText { get; set; } = "";
    [ObservableProperty] public partial string RangeText { get; set; } = "";

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
            if (IsDragging)
            {
                Target = null; // switching tracks mid-drag: end the drag without seeking anything
                IsDragging = false;
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
            ElapsedText = RemainingText = RangeText = "";
            OnPropertyChanged(nameof(Position));
            return;
        }

        Title = t.Title;
        Duration = Math.Max(0.001, t.FileDuration.TotalSeconds);
        RangeStart = t.StartSeconds;
        RangeEnd = t.EndSeconds;
        RangeText = $"{MainViewModel.FmtPrecise(RangeStart)} – {MainViewModel.FmtPrecise(RangeEnd)}";
        FadeIn = t.FadeInSeconds;
        FadeOut = t.FadeOutSeconds;
        ShowFadeOut = t.Track.EndMs is not null; // the engine only auto-fades before an end point
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
