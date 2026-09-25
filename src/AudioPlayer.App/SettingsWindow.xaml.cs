using System.Collections.ObjectModel;
using System.Windows;
using AudioPlayer.App.Input;
using AudioPlayer.App.ViewModels;
using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Model;

namespace AudioPlayer.App;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel vm;
    private readonly ObservableCollection<ShortcutRow> rows;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        this.vm = vm;

        var devices = vm.Devices.RenderDevices();
        MainDevice.ItemsSource = devices;
        MonitorDevice.ItemsSource = devices;
        MainDevice.SelectedValue = vm.Engine.Main.DeviceId;
        MonitorDevice.SelectedValue = vm.Project.MonitorDeviceId;

        var fade = vm.Project.DefaultFade;
        FadeIn.Text = Seconds.Format(fade.FadeInMs);
        FadeOut.Text = Seconds.Format(fade.FadeOutMs);
        Curve.ItemsSource = Enum.GetValues<FadeCurve>();
        Curve.SelectedItem = fade.Curve;

        ActionColumn.ItemsSource = Enum.GetValues<ShortcutAction>();
        rows = new ObservableCollection<ShortcutRow>(vm.Project.Shortcuts.Select(s =>
            new ShortcutRow { Action = s.Action, Gesture = s.Gesture, TrackNumber = s.TrackNumber, Global = s.Global }));
        Shortcuts.ItemsSource = rows;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!Seconds.TryParseMs(FadeIn.Text, out var fadeIn) || !Seconds.TryParseMs(FadeOut.Text, out var fadeOut))
        {
            MessageBox.Show(this, $"Durasi fade harus angka 0–{Seconds.Max} detik, mis. 0.4", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mainId = (string?)MainDevice.SelectedValue;
        var monitorId = (string?)MonitorDevice.SelectedValue;
        if (mainId is not null && mainId == monitorId &&
            MessageBox.Show(this, "Main dan Monitor memakai device yang sama — preview akan terdengar oleh audiens. Lanjut?",
                "Peringatan", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var problems = new List<string>();
        foreach (var r in rows)
        {
            r.Gesture ??= "";
            if (r.Gesture.Trim() != "" && !Gesture.TryParse(r.Gesture, out _)) problems.Add($"Tombol tidak valid: '{r.Gesture}'");
            if (r.Action == ShortcutAction.PlayTrack && r.TrackNumber is not > 0) problems.Add($"'{r.Gesture}': PlayTrack butuh Track # ≥ 1");
        }
        problems.AddRange(rows.Where(r => r.Gesture.Trim() != "").GroupBy(r => r.Gesture.Replace(" ", "").ToLowerInvariant())
            .Where(g => g.Count() > 1).Select(g => $"Tombol dipakai lebih dari sekali: '{g.First().Gesture}'"));
        if (problems.Count > 0)
        {
            MessageBox.Show(this, string.Join("\n", problems), "Shortcut", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        vm.Project.Shortcuts = rows.Select(r =>
            new ShortcutBinding(r.Action, r.Gesture.Trim(), r.Global, r.Action == ShortcutAction.PlayTrack ? r.TrackNumber : null)).ToList();

        vm.ApplySettings(mainId, monitorId,
            new FadeSettings { FadeInMs = fadeIn, FadeOutMs = fadeOut, Curve = (FadeCurve)Curve.SelectedItem });
        DialogResult = true;
    }
}

public sealed class ShortcutRow
{
    public ShortcutAction Action { get; set; }
    public string Gesture { get; set; } = "";
    public int? TrackNumber { get; set; }
    public bool Global { get; set; }
}
