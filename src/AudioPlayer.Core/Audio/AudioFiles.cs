namespace AudioPlayer.Core.Audio;

public static class AudioFiles
{
    public static readonly string[] Extensions = [".wav", ".mp3", ".aif", ".aiff", ".flac"];

    /// <summary>Supported audio files in a folder and its subfolders, sorted by path.</summary>
    public static IEnumerable<string> InFolder(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(p => Extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);
}
