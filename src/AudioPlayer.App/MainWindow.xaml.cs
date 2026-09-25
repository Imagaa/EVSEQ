using System.Windows;
using AudioPlayer.App.Input;
using AudioPlayer.App.ViewModels;
using Microsoft.Win32;

namespace AudioPlayer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel vm = new();
    private ShortcutDispatcher? shortcuts;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += (_, _) =>
        {
            shortcuts = new ShortcutDispatcher(this, vm);
            ReloadShortcuts();
            TrackList.Focus();
        };
        Closed += (_, _) =>
        {
            shortcuts?.Dispose();
            vm.Dispose();
        };
    }

    private void ReloadShortcuts()
    {
        var errors = shortcuts!.Reload();
        if (errors.Count > 0) vm.Status = string.Join("  ", errors);
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Audio|*.wav;*.mp3;*.aif;*.aiff;*.flac|Semua file|*.*",
        };
        if (dlg.ShowDialog(this) == true) vm.AddFiles(dlg.FileNames);
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (new SettingsWindow(vm) { Owner = this }.ShowDialog() == true) ReloadShortcuts();
    }
}
