using NAudio.Vorbis;
using NAudio.Wave;

namespace AudioPlayer.Core.Audio;

public static class AudioFiles
{
    public static readonly string[] Extensions = [".wav", ".mp3", ".aif", ".aiff", ".flac", ".ogg"];

    /// <summary>Supported audio files in a folder and its subfolders, sorted by path.</summary>
    public static IEnumerable<string> InFolder(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(p => Extensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Opens a file as a float stream that is also readable as samples. Windows Media Foundation has no
    /// Vorbis decoder, so .ogg goes through NVorbis; everything else through AudioFileReader.
    /// </summary>
    public static WaveStream OpenReader(string path) =>
        string.Equals(Path.GetExtension(path), ".ogg", StringComparison.OrdinalIgnoreCase)
            ? new VorbisWaveReader(path)
            : new AudioFileReader(path);
}
