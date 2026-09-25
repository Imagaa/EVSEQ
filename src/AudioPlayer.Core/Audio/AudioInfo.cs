using NAudio.Wave;

namespace AudioPlayer.Core.Audio;

public static class AudioInfo
{
    public static TimeSpan GetDuration(string path)
    {
        using var r = AudioFiles.OpenReader(path);
        return r.TotalTime;
    }
}
