using System.Windows.Input;

namespace AudioPlayer.App.Input;

/// <summary>Parses "Ctrl+Alt+D1", "Shift+F5", "Space" (modifiers + a System.Windows.Input.Key name).</summary>
public readonly record struct Gesture(ModifierKeys Modifiers, Key Key)
{
    public static bool TryParse(string? text, out Gesture gesture)
    {
        gesture = default;
        var parts = (text ?? "").Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        var mods = ModifierKeys.None;
        foreach (var p in parts[..^1])
        {
            switch (p.ToLowerInvariant())
            {
                case "ctrl": mods |= ModifierKeys.Control; break;
                case "alt": mods |= ModifierKeys.Alt; break;
                case "shift": mods |= ModifierKeys.Shift; break;
                case "win": mods |= ModifierKeys.Windows; break;
                default: return false;
            }
        }

        var last = parts[^1];
        // Enum.TryParse accepts raw numbers ("1" would become Key.Cancel); digits must be written D1..D9.
        if (char.IsDigit(last[0]) || !Enum.TryParse(last, ignoreCase: true, out Key key) || key == Key.None) return false;

        gesture = new Gesture(mods, key);
        return true;
    }
}
