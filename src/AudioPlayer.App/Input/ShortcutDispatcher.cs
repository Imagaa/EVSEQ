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
    private readonly List<(Gesture Gesture, ShortcutBinding Binding)> local = [];

    public ShortcutDispatcher(Window window, MainViewModel vm)
    {
        this.window = window;
        this.vm = vm;
        hotkeys = new GlobalHotkeys(window);
        window.PreviewKeyDown += OnPreviewKeyDown;
    }

    public IReadOnlyList<string> Reload()
    {
        var errors = new List<string>();
        hotkeys.UnregisterAll();
        local.Clear();
        foreach (var b in vm.Project.Shortcuts)
        {
            if (!Gesture.TryParse(b.Gesture, out var g))
                errors.Add($"Shortcut tidak valid: '{b.Gesture}'.");
            else if (!b.Global)
                local.Add((g, b));
            else if (!hotkeys.Register(g, () => Execute(b)))
                errors.Add($"'{b.Gesture}' sudah dipakai aplikasi lain.");
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
        foreach (var (g, b) in local)
        {
            if (g.Key != key || g.Modifiers != Keyboard.Modifiers) continue;
            Execute(b);
            e.Handled = true;
            return;
        }
    }
}
