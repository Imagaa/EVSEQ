using System.IO;
using System.Text.Json;

namespace AudioPlayer.App.ViewModels;

/// <summary>Per-machine preferences (not per project), stored in %LOCALAPPDATA%\EVSEQ\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>Check GitHub for a newer release at startup and download it in the background.</summary>
    public bool AutoUpdate { get; set; } = true;

    public static AppSettings Load(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings(); // missing or unreadable: defaults
        }
    }

    public void Save(string file)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // a preference that fails to save just falls back to the default next time
        }
    }
}
