using System.Text.Json.Serialization;

namespace AudioPlayer.Core.Model;

public sealed class Track
{
    public string FilePath { get; set; } = "";
    public string Title { get; set; } = "";
    public double VolumeDb { get; set; }
    public bool Loop { get; set; }
    public bool Overlay { get; set; }

    /// <summary>Null means the project's DefaultFade applies.</summary>
    public int? FadeInMs { get; set; }

    /// <summary>Null means the project's DefaultFade applies.</summary>
    public int? FadeOutMs { get; set; }

    /// <summary>Gesture that plays this track on Main, e.g. "F1" or "Ctrl+Shift+J". Null = none.</summary>
    public string? Shortcut { get; set; }

    /// <summary>Shortcut works system-wide, even when the window is not focused.</summary>
    public bool ShortcutGlobal { get; set; }

    /// <summary>Path relative to the project file; written and resolved by ProjectSerializer only.</summary>
    public string? RelativePath { get; set; }

    [JsonIgnore] public bool IsMissing => !File.Exists(FilePath);
}
