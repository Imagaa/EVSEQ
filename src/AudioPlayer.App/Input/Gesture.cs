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

    /// <summary>The gesture for a key press, or null while only modifier keys are down.</summary>
    public static Gesture? FromKeyEvent(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
            return null;
        return new Gesture(Keyboard.Modifiers, key);
    }

    /// <summary>Same format TryParse accepts, e.g. "Ctrl+Shift+F1".</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}
