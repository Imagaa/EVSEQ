using System.Text.Json;

namespace AudioPlayer.Core.Model;

/// <summary>Most-recently-used project paths, newest first, persisted as a JSON string array.</summary>
public sealed class RecentProjects(string file, int max = 10)
{
    /// <summary>Stored paths that still exist on disk.</summary>
    public IReadOnlyList<string> Load() => Read().Where(File.Exists).ToList();

    public void Add(string projectPath)
    {
        var full = Path.GetFullPath(projectPath);
        var list = Read();
        list.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, full);
        if (list.Count > max) list.RemoveRange(max, list.Count - max);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        File.WriteAllText(file, JsonSerializer.Serialize(list));
    }

    private List<string> Read()
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return []; // missing or corrupt list is not worth failing startup over
        }
    }
}
