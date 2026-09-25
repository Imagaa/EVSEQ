namespace AudioPlayer.Core.Model;

/// <summary>
/// Crash recovery: a lock file marks a running session; an autosave holds unsaved work.
/// Both present at startup means the previous session did not exit cleanly.
/// </summary>
public sealed class SessionRecovery(string directory)
{
    private string LockPath => Path.Combine(directory, "session.lock");
    private string AutosavePath => Path.Combine(directory, "autosave.approj");

    public bool PreviousSessionCrashed => File.Exists(LockPath) && File.Exists(AutosavePath);

    public void BeginSession()
    {
        Directory.CreateDirectory(directory);
        File.Delete(AutosavePath);
        File.WriteAllText(LockPath, "");
    }

    /// <summary>The lock file keeps the real project path so a restore can save back to it.</summary>
    public void Save(Project project, string? projectPath)
    {
        Directory.CreateDirectory(directory);
        ProjectSerializer.Save(project, AutosavePath);
        File.WriteAllText(LockPath, projectPath ?? "");
    }

    public void DiscardAutosave() => File.Delete(AutosavePath);

    public (Project Project, string? OriginalPath) Restore()
    {
        var original = File.ReadAllText(LockPath);
        return (ProjectSerializer.Load(AutosavePath), original.Length == 0 ? null : original);
    }

    public void EndSession()
    {
        File.Delete(AutosavePath);
        File.Delete(LockPath);
    }
}
