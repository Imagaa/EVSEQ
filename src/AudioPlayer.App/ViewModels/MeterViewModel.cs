using AudioPlayer.Core.Audio;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPlayer.App.ViewModels;

/// <summary>Stereo peak meter state for one bus, fed ~30×/s with the bus's measured peaks.</summary>
public sealed partial class MeterViewModel : ObservableObject
{
    private readonly MeterBallistics left = new(), right = new();

    [ObservableProperty] public partial double LeftDb { get; set; } = MeterBallistics.FloorDb;
    [ObservableProperty] public partial double RightDb { get; set; } = MeterBallistics.FloorDb;
    [ObservableProperty] public partial double LeftHoldDb { get; set; } = MeterBallistics.FloorDb;
    [ObservableProperty] public partial double RightHoldDb { get; set; } = MeterBallistics.FloorDb;
    [ObservableProperty] public partial bool Clipped { get; set; }

    /// <summary>Highest held peak of both channels, e.g. "-3.2 dB".</summary>
    [ObservableProperty] public partial string PeakText { get; set; } = "−∞";

    /// <returns>True when this update started a new clip (for logging).</returns>
    public bool Update((float Left, float Right) peaks, double elapsedSeconds)
    {
        bool wasClipped = Clipped;
        left.Update(peaks.Left, elapsedSeconds);
        right.Update(peaks.Right, elapsedSeconds);
        LeftDb = left.LevelDb;
        RightDb = right.LevelDb;
        LeftHoldDb = left.HoldDb;
        RightHoldDb = right.HoldDb;
        Clipped = left.Clipped || right.Clipped;
        double max = Math.Max(left.HoldDb, right.HoldDb);
        PeakText = max <= MeterBallistics.FloorDb + 0.05 ? "−∞" : $"{max:0.0} dB";
        return Clipped && !wasClipped;
    }

    [RelayCommand]
    private void ResetClip()
    {
        left.ResetClip();
        right.ResetClip();
        Clipped = false;
    }
}
