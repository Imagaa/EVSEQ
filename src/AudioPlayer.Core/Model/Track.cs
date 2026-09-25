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
    public FadeSettings? Fade { get; set; }

    /// <summary>Path relative to the project file; written and resolved by ProjectSerializer only.</summary>
    public string? RelativePath { get; set; }

    [JsonIgnore] public bool IsMissing => !File.Exists(FilePath);
}
