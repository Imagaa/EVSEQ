using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AudioPlayer.App.Input;
using AudioPlayer.App.ViewModels;
using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Model;
using Microsoft.Win32;

namespace AudioPlayer.App;

public partial class MainWindow : Window
{
    private const string ProjectFilter = "Project Audio Player (*.approj)|*.approj";

    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AudioPlayer");

    private readonly MainViewModel vm = new();
    private readonly SessionRecovery recovery = new(AppDataDir);
    private readonly RecentProjects recent = new(Path.Combine(AppDataDir, "recent.json"));
    private readonly DispatcherTimer autosave = new() { Interval = TimeSpan.FromSeconds(60) };
    private ShortcutDispatcher? shortcuts;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += (_, _) =>
        {
            OfferRecovery();
            recovery.BeginSession();
            autosave.Tick += (_, _) => Autosave();
            autosave.Start();

            shortcuts = new ShortcutDispatcher(this, vm);
            ReloadShortcuts();
            RefreshRecent();
            FocusMain();
        };
        Closing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
        Closed += (_, _) =>
        {
            autosave.Stop();
            recovery.EndSession();
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
        if (dlg.ShowDialog(this) != true) return;
        vm.AddFiles(dlg.FileNames);
        FocusMain();
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Pilih folder berisi file audio" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var files = AudioFiles.InFolder(dlg.FolderName).ToList();
            if (files.Count == 0)
            {
                vm.Status = $"Tidak ada file audio ({string.Join(", ", AudioFiles.Extensions)}) di folder itu.";
                return;
            }
            vm.AddFiles(files);
            FocusMain();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            vm.Status = $"Gagal membaca folder: {ex.Message}";
        }
    }

    private void OpenRecent_Click(object sender, RoutedEventArgs e)
    {
        if (ConfirmDiscard()) OpenProject((string)((FrameworkElement)sender).Tag);
    }

    private void RefreshRecent()
    {
        var items = recent.Load()
            .Select(p => new { Name = Path.GetFileNameWithoutExtension(p), Folder = Path.GetDirectoryName(p), Path = p })
            .ToList();
        RecentList.ItemsSource = items;
        NoRecent.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Keyboard focus on the welcome screen when it is showing, otherwise on the track list.</summary>
    private void FocusMain()
    {
        if (vm.ShowWelcome)
        {
            WelcomeAddFiles.Focus();
            return;
        }
        vm.Selected ??= vm.Tracks[0];
        TrackList.UpdateLayout();
        (TrackList.ItemContainerGenerator.ContainerFromItem(vm.Selected) as UIElement ?? TrackList).Focus();
    }

    private void RememberRecent(string path)
    {
        try
        {
            recent.Add(path);
            RefreshRecent();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // the recent list is a convenience; never fail a save/open over it
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (new SettingsWindow(vm) { Owner = this }.ShowDialog() == true) ReloadShortcuts();
    }

    private void New_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        vm.Load(new Project(), null);
        ReloadShortcuts();
        RefreshRecent();
        FocusMain();
    }

    private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = ProjectFilter };
        if (dlg.ShowDialog(this) == true) OpenProject(dlg.FileName);
    }

    private void OpenProject(string path)
    {
        try
        {
            vm.Load(ProjectSerializer.Load(path), path);
            ReloadShortcuts();
            RememberRecent(path);
            FocusMain();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            MessageBox.Show(this, $"Gagal membuka project:\n{ex.Message}", "Buka project", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Save_Executed(object sender, ExecutedRoutedEventArgs e) => Save();
    private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e) => SaveAs();
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private bool Save() => vm.ProjectPath is null ? SaveAs() : SaveTo(vm.ProjectPath);

    private bool SaveAs()
    {
        var dlg = new SaveFileDialog { Filter = ProjectFilter, FileName = "project.approj" };
        return dlg.ShowDialog(this) == true && SaveTo(dlg.FileName);
    }

    private bool SaveTo(string path)
    {
        try
        {
            ProjectSerializer.Save(vm.Project, path);
            vm.MarkSaved(path);
            recovery.DiscardAutosave();
            RememberRecent(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Gagal menyimpan project:\n{ex.Message}", "Simpan", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>True when it is safe to replace/close the current project.</summary>
    private bool ConfirmDiscard()
    {
        if (!vm.IsDirty) return true;
        var r = MessageBox.Show(this, "Simpan perubahan project?", "Audio Player", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return r == MessageBoxResult.No || (r == MessageBoxResult.Yes && Save());
    }

    private void Autosave()
    {
        try
        {
            if (vm.IsDirty) recovery.Save(vm.Project, vm.ProjectPath);
            else recovery.DiscardAutosave();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            vm.Status = $"Autosave gagal: {ex.Message}";
        }
    }

    private void OfferRecovery()
    {
        if (!recovery.PreviousSessionCrashed) return;
        if (MessageBox.Show(this, "Aplikasi tidak ditutup dengan benar. Pulihkan perubahan terakhir yang belum disimpan?",
                "Pemulihan", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            var (project, original) = recovery.Restore();
            vm.Load(project, original);
            vm.MarkDirty(); // restored work is still unsaved
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            MessageBox.Show(this, $"Autosave rusak, tidak bisa dipulihkan:\n{ex.Message}", "Pemulihan", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
