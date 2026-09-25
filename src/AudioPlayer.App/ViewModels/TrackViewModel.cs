using System.Runtime.CompilerServices;
using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Engine;
using AudioPlayer.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPlayer.App.ViewModels;

public sealed partial class TrackViewModel : ObservableObject
{
    private readonly MainViewModel owner;

    public TrackViewModel(Track track, MainViewModel owner)
    {
        Track = track;
        this.owner = owner;
        try
        {
            FileDuration = track.IsMissing ? TimeSpan.Zero : AudioInfo.GetDuration(track.FilePath);
        }
        catch (Exception)
        {
            FileDuration = TimeSpan.Zero; // unreadable/unsupported file; Play will report the error
        }
    }

    public Track Track { get; }

    /// <summary>Length of the whole file.</summary>
    public TimeSpan FileDuration { get; }

    /// <summary>Length actually played: start point to end point.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(Math.Max(0, EndSeconds - StartSeconds));

    public string DurationText => MainViewModel.Fmt(Duration);
    public double StartSeconds => (Track.StartMs ?? 0) / 1000.0;
    public double EndSeconds => Track.EndMs is { } e ? e / 1000.0 : FileDuration.TotalSeconds;
    public bool HasRange => Track.StartMs is not null || Track.EndMs is not null;

    public void SetRange(int? startMs, int? endMs)
    {
        Track.StartMs = startMs;
        Track.EndMs = endMs;
        owner.Engine.Refresh(Track);
        owner.MarkDirty();
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(HasRange));
        owner.RangesChanged();
    }
    public string Title => Track.Title;
    public bool IsMissing => Track.IsMissing;

    [ObservableProperty] public partial int Number { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(PlayLabel))]
    public partial PlayState MainState { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(PreviewLabel))]
    public partial PlayState MonitorState { get; set; }

    [ObservableProperty] public partial string RemainingText { get; set; } = "";

    public string PlayLabel => MainState == PlayState.Playing ? "⏸" : "▶";
    public string PreviewLabel => MonitorState == PlayState.Playing ? "■ Cue" : "Cue";

    public double VolumeDb { get => Track.VolumeDb; set { Track.VolumeDb = value; Changed(); } }
    public bool Loop { get => Track.Loop; set { Track.Loop = value; Changed(); } }
    public bool Overlay { get => Track.Overlay; set { Track.Overlay = value; Changed(); } }

    // Fade per track, edited with sliders (seconds). Moving a slider creates an override;
    // Reset goes back to the project default. The sliders always show the effective value.
    public double FadeInSeconds
    {
        get => (Track.FadeInMs ?? owner.Project.DefaultFade.FadeInMs) / 1000.0;
        set { Track.FadeInMs = (int)Math.Round(value * 1000); FadeChanged(); }
    }

    public double FadeOutSeconds
    {
        get => (Track.FadeOutMs ?? owner.Project.DefaultFade.FadeOutMs) / 1000.0;
        set { Track.FadeOutMs = (int)Math.Round(value * 1000); FadeChanged(); }
    }

    public bool FadeInIsDefault => Track.FadeInMs is null;
    public bool FadeOutIsDefault => Track.FadeOutMs is null;
    public FadeCurve Curve => owner.Project.DefaultFade.Curve;

    [RelayCommand]
    private void ResetFadeIn()
    {
        Track.FadeInMs = null;
        FadeChanged();
    }

    [RelayCommand]
    private void ResetFadeOut()
    {
        Track.FadeOutMs = null;
        FadeChanged();
    }

    /// <summary>The project default changed: tracks without an override follow it.</summary>
    public void DefaultsChanged() => NotifyFades();

    private void FadeChanged()
    {
        owner.Engine.Refresh(Track);
        owner.MarkDirty();
        NotifyFades();
    }

    private void NotifyFades()
    {
        OnPropertyChanged(nameof(FadeInSeconds));
        OnPropertyChanged(nameof(FadeOutSeconds));
        OnPropertyChanged(nameof(FadeInIsDefault));
        OnPropertyChanged(nameof(FadeOutIsDefault));
    }

    /// <summary>Gesture that plays this track on Main ("" = none).</summary>
    public string Shortcut
    {
        get => Track.Shortcut ?? "";
        set { Track.Shortcut = string.IsNullOrEmpty(value) ? null : value; ShortcutChanged(); }
    }

    public bool ShortcutGlobal
    {
        get => Track.ShortcutGlobal;
        set { Track.ShortcutGlobal = value; ShortcutChanged(); }
    }

    private void ShortcutChanged([CallerMemberName] string? name = null)
    {
        owner.MarkDirty();
        OnPropertyChanged(name);
        owner.NotifyShortcutsChanged();
    }

    private void Changed([CallerMemberName] string? name = null)
    {
        owner.Engine.Refresh(Track);
        owner.MarkDirty();
        OnPropertyChanged(name);
    }
}
