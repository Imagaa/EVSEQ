using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AudioPlayer.App.ViewModels;
using AudioPlayer.Core.Model;

namespace AudioPlayer.App.Input;

public sealed class ShortcutDispatcher : IDisposable
{
    private readonly Window window;
    private readonly MainViewModel vm;
    private readonly GlobalHotkeys hotkeys;
    private readonly List<(Gesture Gesture, Action Action)> local = [];

    public ShortcutDispatcher(Window window, MainViewModel vm)
    {
        this.window = window;
        this.vm = vm;
        hotkeys = new GlobalHotkeys(window);
        window.PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Re-registers project shortcuts and per-track shortcuts; returns problems found.</summary>
    public IReadOnlyList<string> Reload()
    {
        var errors = new List<string>();
        var used = new HashSet<Gesture>();
        hotkeys.UnregisterAll();
        local.Clear();

        void Add(string text, bool global, Action action, string owner)
        {
            if (string.IsNullOrWhiteSpace(text)) return; // unassigned
            if (!Gesture.TryParse(text, out var g))
                errors.Add($"Shortcut tidak valid: '{text}' ({owner}).");
            else if (!used.Add(g))
                errors.Add($"'{text}' dipakai lebih dari satu shortcut ({owner} diabaikan).");
            else if (!global)
                local.Add((g, action));
            else if (!hotkeys.Register(g, action))
                errors.Add($"'{text}' sudah dipakai aplikasi lain ({owner}).");
        }

        foreach (var b in vm.Project.Shortcuts)
            Add(b.Gesture, b.Global, () => Execute(b), b.Action.ToString());
        foreach (var t in vm.Tracks)
        {
            if (t.Track.Shortcut is { } s)
                Add(s, t.Track.ShortcutGlobal, () => vm.PlayTrack(t), $"track {t.Number}");
        }
        return errors;
    }

    public void Execute(ShortcutBinding b)
    {
        switch (b.Action)
        {
            case ShortcutAction.PlayPauseSelected: vm.PlayPauseCommand.Execute(null); break;
            case ShortcutAction.PreviewSelected: vm.PreviewCommand.Execute(null); break;
            case ShortcutAction.StopSelected: vm.StopCommand.Execute(null); break;
            case ShortcutAction.ToggleLoopSelected:
                if (vm.Selected is { } t) t.Loop = !t.Loop;
                break;
            case ShortcutAction.PlayTrack:
                if (b.TrackNumber is { } n) vm.PlayNumber(n);
                break;
            case ShortcutAction.Panic: vm.PanicCommand.Execute(null); break;
            case ShortcutAction.MainPlayPause: vm.MainPlayPauseCommand.Execute(null); break;
            case ShortcutAction.MainStop: vm.MainStopCommand.Execute(null); break;
            case ShortcutAction.CuePlayPause: vm.CuePlayPauseCommand.Execute(null); break;
            case ShortcutAction.CueStop: vm.CueStopCommand.Execute(null); break;
        }
    }

    public void Dispose()
    {
        window.PreviewKeyDown -= OnPreviewKeyDown;
        hotkeys.Dispose();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // never hijack typing, and let focused buttons (welcome screen) take Enter/Space
        if (e.OriginalSource is TextBox or ButtonBase) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        foreach (var (g, action) in local)
        {
            if (g.Key != key || g.Modifiers != Keyboard.Modifiers) continue;
            action();
            e.Handled = true;
            return;
        }
    }
}
