using System.Text.Json;
using System.Text.Json.Serialization;

namespace AudioPlayer.Core.Model;

public static class ProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(Project project, string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        foreach (var t in project.Tracks)
            t.RelativePath = Path.GetRelativePath(dir, t.FilePath);

        // Write to a temp file first so a crash mid-save never corrupts the existing project.
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(project, Options));
        File.Move(tmp, path, overwrite: true);
    }

    public static Project Load(string path)
    {
        var project = JsonSerializer.Deserialize<Project>(File.ReadAllText(path), Options)
                      ?? throw new InvalidDataException($"'{path}' is not a project file.");
        var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        foreach (var t in project.Tracks)
        {
            if (t.RelativePath is null) continue;
            var candidate = Path.GetFullPath(Path.Combine(dir, t.RelativePath));
            if (File.Exists(candidate)) t.FilePath = candidate;
        }
        return project;
    }
}
