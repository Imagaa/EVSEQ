using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace AudioPlayer.App.Input;

/// <summary>System-wide hotkeys via RegisterHotKey; fire even when the window is not focused.</summary>
public sealed partial class GlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly HwndSource source;
    private readonly Dictionary<int, Action> actions = [];
    private int nextId = 1;

    public GlobalHotkeys(Window window)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());
        source.AddHook(Hook);
    }

    public bool Register(Gesture g, Action action)
    {
        int id = nextId++;
        // ModifierKeys values (Alt=1, Control=2, Shift=4, Windows=8) match the Win32 MOD_* flags.
        uint mods = ModNoRepeat | (uint)g.Modifiers;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(g.Key);
        if (!RegisterHotKey(source.Handle, id, mods, vk)) return false;
        actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in actions.Keys) UnregisterHotKey(source.Handle, id);
        actions.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        source.RemoveHook(Hook);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
