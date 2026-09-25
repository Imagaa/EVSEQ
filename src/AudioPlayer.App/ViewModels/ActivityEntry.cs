namespace AudioPlayer.App.ViewModels;

public enum ActivityKind { Info, Success, Warning, Error }

/// <summary>One line in the Activity panel.</summary>
public sealed record ActivityEntry(DateTime Time, string Text, ActivityKind Kind)
{
    public string TimeText => Time.ToString("HH:mm:ss");
}
