namespace AudioPlayer.Core.Model;

public enum ShortcutAction
{
    PlayPauseSelected, PreviewSelected, StopSelected, ToggleLoopSelected, PlayTrack, Panic,
    MainPlayPause, MainStop, CuePlayPause, CueStop,
}

/// <param name="Gesture">Key name from System.Windows.Input.Key with modifiers, e.g. "Ctrl+Alt+D1", "Space", "F12".</param>
/// <param name="Global">Registered as a system-wide hotkey (works when the window is not focused).</param>
/// <param name="TrackNumber">1-based list position; only for PlayTrack.</param>
public sealed record ShortcutBinding(ShortcutAction Action, string Gesture, bool Global = false, int? TrackNumber = null);

public static class DefaultShortcuts
{
    public static List<ShortcutBinding> Create() =>
    [
        new(ShortcutAction.PlayPauseSelected, "Space"),
        new(ShortcutAction.PreviewSelected, "Enter"),
        new(ShortcutAction.StopSelected, "S"),
        new(ShortcutAction.ToggleLoopSelected, "L"),
        new(ShortcutAction.Panic, "F12"),
        new(ShortcutAction.MainPlayPause, "F5"),
        new(ShortcutAction.MainStop, "F6"),
        new(ShortcutAction.CuePlayPause, "F7"),
        new(ShortcutAction.CueStop, "F8"),
        .. Enumerable.Range(1, 9).Select(i =>
            new ShortcutBinding(ShortcutAction.PlayTrack, $"Ctrl+Alt+D{i}", Global: true, TrackNumber: i)),
    ];
}
