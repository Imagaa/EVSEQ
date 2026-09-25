using System.Runtime.CompilerServices;
using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Engine;
using AudioPlayer.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;

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
            Duration = track.IsMissing ? TimeSpan.Zero : AudioInfo.GetDuration(track.FilePath);
        }
        catch (Exception)
        {
            Duration = TimeSpan.Zero; // unreadable/unsupported file; Play will report the error
        }
    }

    public Track Track { get; }
    public TimeSpan Duration { get; }
    public string DurationText => MainViewModel.Fmt(Duration);
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

    private void Changed([CallerMemberName] string? name = null)
    {
        owner.Engine.Refresh(Track);
        owner.MarkDirty();
        OnPropertyChanged(name);
    }
}
