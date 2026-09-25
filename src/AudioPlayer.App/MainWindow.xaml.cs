using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AudioPlayer.App.Input;
using AudioPlayer.App.ViewModels;
using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Model;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using Microsoft.Win32;

namespace AudioPlayer.App;

public partial class MainWindow : Window
{
    private const string ProjectFilter = "Project EVSEQ (*.approj)|*.approj";

    private static readonly string AppDataDir = AppPaths.DataDir;

    private readonly MainViewModel vm = new();
    private bool relaunchAfterUpdate;
    private readonly SessionRecovery recovery = new(AppDataDir);
    private readonly RecentProjects recent = new(Path.Combine(AppDataDir, "recent.json"));
    private readonly DispatcherTimer autosave = new() { Interval = TimeSpan.FromSeconds(60) };
    private static readonly string LayoutPath = Path.Combine(AppDataDir, "layout.xml");
    private ShortcutDispatcher? shortcuts;
    private Dictionary<string, (string Title, object Content)>? panels; // ContentId -> panel, reused across layout loads
    private string? defaultLayout;

    public MainWindow(string? startupProject = null)
    {
        InitializeComponent();
        DataContext = vm;
        // Panels may be floated into their own windows, so they get the view model directly
        // instead of inheriting it from this window.
        foreach (var p in new FrameworkElement[] { TracksPanel, MasterPanel, MainPanel, CuePanel, ActivityPanel })
            p.DataContext = vm;
        MidiPanel.DataContext = vm.Midi;
        vm.ShortcutsChanged += () => { if (shortcuts is not null) ReloadShortcuts(); };
        vm.Update.InstallNowRequested += () =>
        {
            relaunchAfterUpdate = true;
            Close(); // asks to save first; cancelling keeps EVSEQ open and the update waiting
        };
        Loaded += async (_, _) =>
        {
            panels = Dock.Layout.Descendents().OfType<LayoutAnchorable>().ToDictionary(a => a.ContentId, a => (a.Title, a.Content));
            defaultLayout = SerializeLayout();
            RestoreSavedLayout();
            OfferRecovery();
            recovery.BeginSession();
            autosave.Tick += (_, _) => Autosave();
            autosave.Start();

            shortcuts = new ShortcutDispatcher(this, vm);
            ReloadShortcuts();
            RefreshRecent();
            // Only once everything above is in place; recovered unsaved work takes precedence.
            if (startupProject is not null && !vm.IsDirty) OpenProject(Path.GetFullPath(startupProject));
            FocusMain();

            // Update check after startup has settled; silent when offline.
            await Task.Delay(TimeSpan.FromSeconds(5));
            await vm.Update.CheckAsync(userInitiated: false);
        };
        Closing += (_, e) =>
        {
            if (!ConfirmDiscard())
            {
                e.Cancel = true;
                relaunchAfterUpdate = false;
            }
        };
        Closed += (_, _) =>
        {
            SaveLayout();
            autosave.Stop();
            recovery.EndSession();
            shortcuts?.Dispose();
            vm.Dispose();
            // A downloaded, verified update is installed now that EVSEQ has closed.
            vm.Update.LaunchInstaller(relaunchAfterUpdate);
        };
    }

    // ---- Dock layout: remembered between runs, with a reset to the built-in default ----

    private string SerializeLayout()
    {
        using var w = new StringWriter();
        new XmlLayoutSerializer(Dock).Serialize(w);
        return w.ToString();
    }

    /// <summary>
    /// Loads a layout, re-attaching the existing panel contents. Panels the layout does not know
    /// (added in a newer version) are docked next to the Activity panel, so none is ever lost.
    /// </summary>
    private void LoadLayout(string xml)
    {
        foreach (var a in Dock.Layout.Descendents().OfType<LayoutAnchorable>().ToList()) a.Content = null;
        var serializer = new XmlLayoutSerializer(Dock);
        serializer.LayoutSerializationCallback += (_, e) =>
        {
            if (e.Model.ContentId is { } id && panels!.TryGetValue(id, out var panel)) e.Content = panel.Content;
            else e.Cancel = true;
        };
        using (var r = new StringReader(xml)) serializer.Deserialize(r);

        var anchorables = Dock.Layout.Descendents().OfType<LayoutAnchorable>().ToList();
        var loaded = anchorables.Select(a => a.ContentId).ToHashSet();
        var activityParent = anchorables.FirstOrDefault(a => a.ContentId == "activity")?.Parent;
        foreach (var (id, panel) in panels!.Where(p => !loaded.Contains(p.Key)))
        {
            var added = new LayoutAnchorable { ContentId = id, Title = panel.Title, Content = panel.Content, CanClose = false, CanHide = false };
            switch (activityParent)
            {
                case LayoutAnchorablePane pane: pane.Children.Add(added); break;   // docked: new tab beside Activity
                case LayoutAnchorGroup side: side.Children.Add(added); break;      // auto-hidden: same edge as Activity
                default:
                    (Dock.Layout.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault()
                     ?? throw new InvalidDataException("Layout tanpa panel.")).Children.Add(added);
                    break;
            }
        }
    }

    private void RestoreSavedLayout()
    {
        if (!File.Exists(LayoutPath)) return;
        try
        {
            LoadLayout(File.ReadAllText(LayoutPath));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or System.Xml.XmlException)
        {
            LoadLayout(defaultLayout!); // a broken or outdated layout must never hide a panel
            vm.Log("Layout tersimpan tidak valid, memakai layout default", ActivityKind.Warning);
        }
    }

    private void SaveLayout()
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(LayoutPath, SerializeLayout());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // losing the layout only means starting from the default next time
        }
    }

    private void ResetLayout_Click(object sender, RoutedEventArgs e)
    {
        LoadLayout(defaultLayout!);
        vm.Log("Layout panel dikembalikan ke default");
    }

    // Newly plugged interfaces/controllers show up without restarting.
    private void Outputs_DropDownOpened(object? sender, EventArgs e) => vm.RefreshOutputDevices();

    private void MidiPorts_DropDownOpened(object? sender, EventArgs e) => vm.Midi.RefreshPorts();

    /// <summary>
    /// Records the pressed key combination into a shortcut box. Works for any box whose Text binding
    /// uses UpdateSourceTrigger=Explicit (track shortcuts, seek-bar transport keys).
    /// </summary>
    private void ShortcutBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab) return; // keep keyboard navigation
        e.Handled = true;
        var box = (TextBox)sender;
        if (e.Key == Key.Escape)
        {
            FocusMain();
            return;
        }
        string? text = e.Key is Key.Back or Key.Delete && Keyboard.Modifiers == ModifierKeys.None
            ? ""
            : Gesture.FromKeyEvent(e)?.ToString();
        if (text is null) return; // only modifiers held so far
        box.Text = text;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        FocusMain(); // one key press = one assignment; the next key must not overwrite it
    }

    private void ReloadShortcuts()
    {
        if (shortcuts is null) return; // not wired yet; Loaded loads them once it is
        var errors = shortcuts.Reload();
        if (errors.Count > 0) vm.Status = string.Join("  ", errors);
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = $"Audio|{string.Join(";", AudioFiles.Extensions.Select(e => "*" + e))}|Semua file|*.*",
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
            vm.NoteSaved(path);
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
        var r = MessageBox.Show(this, "Simpan perubahan project?", "EVSEQ", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return r == MessageBoxResult.No || (r == MessageBoxResult.Yes && Save());
    }

    private void Autosave()
    {
        try
        {
            if (vm.IsDirty)
            {
                recovery.Save(vm.Project, vm.ProjectPath);
                vm.NoteAutosaved();
            }
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
