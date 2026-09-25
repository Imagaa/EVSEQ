using System.IO;

namespace AudioPlayer.App;

public static class AppPaths
{
    /// <summary>
    /// %LOCALAPPDATA%\EVSEQ: recent list, layout, autosave, settings, downloaded updates. Data from before
    /// the rename (\AudioPlayer) is moved over once so nothing is lost.
    /// </summary>
    public static string DataDir { get; } = MigrateDataDir();

    private static string MigrateDataDir()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = Path.Combine(root, "EVSEQ"), old = Path.Combine(root, "AudioPlayer");
        if (Directory.Exists(dir) || !Directory.Exists(old)) return dir;
        try
        {
            Directory.Move(old, dir);
            return dir;
        }
        catch (IOException)
        {
            return old; // folder in use; keep using it rather than start empty
        }
    }
}
